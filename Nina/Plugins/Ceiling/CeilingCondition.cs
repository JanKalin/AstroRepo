using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Newtonsoft.Json;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;

namespace Ceiling
{
    public readonly struct AltAzPoint
    {
        public double Azimuth { get; }
        public double Altitude { get; }

        public AltAzPoint(double azimuth, double altitude)
        {
            Azimuth = NormalizeAzimuth(azimuth);
            Altitude = altitude;
        }

        public static double NormalizeAzimuth(double azimuth)
        {
            return (azimuth % 360.0 + 360.0) % 360.0;
        }

        public static double ShortestAzimuthDelta(double fromAzimuth, double toAzimuth)
        {
            double delta = NormalizeAzimuth(toAzimuth) - NormalizeAzimuth(fromAzimuth);
            if (delta > 180.0) delta -= 360.0;
            if (delta < -180.0) delta += 360.0;
            return delta;
        }
    }

    public static class CeilingBoundaryChecker
    {
        public static double GetCeilingAltitude(
            double currentAzimuth,
            IReadOnlyList<AltAzPoint> points)
        {
            if (points == null || points.Count < 2)
            {
                return double.NaN;
            }

            var sorted = points.OrderBy(p => p.Azimuth).ToList();
            double currentAz = AltAzPoint.NormalizeAzimuth(currentAzimuth);

            AltAzPoint p1 = sorted[^1];
            AltAzPoint p2 = sorted[0];

            for (int i = 0; i < sorted.Count; i++)
            {
                if (sorted[i].Azimuth >= currentAz)
                {
                    p2 = sorted[i];
                    p1 = i == 0 ? sorted[^1] : sorted[i - 1];
                    break;
                }
            }

            double azSpan = p2.Azimuth - p1.Azimuth;
            if (azSpan < 0) azSpan += 360.0;

            double azOffset = currentAz - p1.Azimuth;
            if (azOffset < 0) azOffset += 360.0;

            double fraction = azSpan > 1e-9 ? azOffset / azSpan : 0.0;
            return p1.Altitude + fraction * (p2.Altitude - p1.Altitude);
        }

        public static bool IsAboveCeiling(
            double currentAzimuth,
            double currentAltitude,
            IReadOnlyList<AltAzPoint> points)
        {
            double ceilingAltitude = GetCeilingAltitude(currentAzimuth, points);
            return double.IsFinite(ceilingAltitude) && currentAltitude > ceilingAltitude;
        }
    }

    internal sealed class CeilingPrediction
    {
        public bool Available { get; init; }
        public bool AlreadyAbove { get; init; }
        public bool HasCrossing { get; init; }
        public TimeSpan TimeToCrossing { get; init; }
        public DateTime CrossingTimeLocal { get; init; }
        public double CrossingAzimuth { get; init; } = double.NaN;
        public double CrossingAltitude { get; init; } = double.NaN;
        public string Method { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
    }

    internal readonly struct MotionSample
    {
        public DateTime Utc { get; }
        public double Azimuth { get; }
        public double Altitude { get; }

        public MotionSample(DateTime utc, double azimuth, double altitude)
        {
            Utc = utc;
            Azimuth = azimuth;
            Altitude = altitude;
        }
    }

    internal static class CeilingPredictor
    {
        private const double SiderealHoursPerSolarHour = 1.00273790935;
        private const double DegreesToRadians = Math.PI / 180.0;
        private const double RadiansToDegrees = 180.0 / Math.PI;

        public static CeilingPrediction PredictFixedEquatorialTrack(
            double currentAzimuth,
            double currentAltitude,
            double rightAscensionHours,
            double declinationDegrees,
            double localSiderealTimeHours,
            double siteLatitudeDegrees,
            IReadOnlyList<AltAzPoint> boundary,
            TimeSpan horizon)
        {
            if (!double.IsFinite(rightAscensionHours) || rightAscensionHours < 0 || rightAscensionHours >= 24 ||
                !double.IsFinite(declinationDegrees) || declinationDegrees < -90 || declinationDegrees > 90 ||
                !double.IsFinite(localSiderealTimeHours) || localSiderealTimeHours < 0 || localSiderealTimeHours >= 24 ||
                !double.IsFinite(siteLatitudeDegrees) || siteLatitudeDegrees < -90 || siteLatitudeDegrees > 90)
            {
                return new CeilingPrediction
                {
                    Available = false,
                    Reason = "mount did not provide usable RA/Dec, sidereal time, or site latitude"
                };
            }

            AltAzPoint rawNow = EquatorialToHorizontal(
                rightAscensionHours,
                declinationDegrees,
                localSiderealTimeHours,
                siteLatitudeDegrees);

            // Anchor the analytical path to the mount's measured current Alt/Az.
            // This absorbs small epoch/refraction/driver convention offsets while
            // preserving the curvature of the predicted sky path.
            double azCorrection = AltAzPoint.ShortestAzimuthDelta(rawNow.Azimuth, currentAzimuth);
            double altCorrection = currentAltitude - rawNow.Altitude;

            AltAzPoint PositionAt(TimeSpan elapsed)
            {
                double futureLst = NormalizeHours(
                    localSiderealTimeHours +
                    elapsed.TotalHours * SiderealHoursPerSolarHour);

                AltAzPoint raw = EquatorialToHorizontal(
                    rightAscensionHours,
                    declinationDegrees,
                    futureLst,
                    siteLatitudeDegrees);

                return new AltAzPoint(
                    raw.Azimuth + azCorrection,
                    raw.Altitude + altCorrection);
            }

            return FindFirstCrossing(
                PositionAt,
                boundary,
                horizon,
                "sidereal sky-path model");
        }

        public static CeilingPrediction PredictLinearMotion(
            MotionSample older,
            MotionSample newer,
            IReadOnlyList<AltAzPoint> boundary,
            TimeSpan horizon)
        {
            double dt = (newer.Utc - older.Utc).TotalSeconds;
            if (dt < 10.0 || dt > 20.0 * 60.0)
            {
                return new CeilingPrediction
                {
                    Available = false,
                    Reason = "need two recent telescope-position samples"
                };
            }

            double azRate = AltAzPoint.ShortestAzimuthDelta(older.Azimuth, newer.Azimuth) / dt;
            double altRate = (newer.Altitude - older.Altitude) / dt;

            if (!double.IsFinite(azRate) || !double.IsFinite(altRate))
            {
                return new CeilingPrediction
                {
                    Available = false,
                    Reason = "measured telescope motion is invalid"
                };
            }

            AltAzPoint PositionAt(TimeSpan elapsed)
            {
                double seconds = elapsed.TotalSeconds;
                return new AltAzPoint(
                    newer.Azimuth + azRate * seconds,
                    newer.Altitude + altRate * seconds);
            }

            return FindFirstCrossing(
                PositionAt,
                boundary,
                horizon,
                "measured-motion extrapolation");
        }

        private static CeilingPrediction FindFirstCrossing(
            Func<TimeSpan, AltAzPoint> positionAt,
            IReadOnlyList<AltAzPoint> boundary,
            TimeSpan horizon,
            string method)
        {
            AltAzPoint now = positionAt(TimeSpan.Zero);
            double nowCeiling = CeilingBoundaryChecker.GetCeilingAltitude(now.Azimuth, boundary);
            double previousGap = now.Altitude - nowCeiling;

            if (double.IsFinite(previousGap) && previousGap >= 0.0)
            {
                return new CeilingPrediction
                {
                    Available = true,
                    AlreadyAbove = true,
                    HasCrossing = true,
                    TimeToCrossing = TimeSpan.Zero,
                    CrossingTimeLocal = DateTime.Now,
                    CrossingAzimuth = now.Azimuth,
                    CrossingAltitude = now.Altitude,
                    Method = method
                };
            }

            TimeSpan coarseStep = TimeSpan.FromMinutes(1);
            TimeSpan previousTime = TimeSpan.Zero;

            for (TimeSpan currentTime = coarseStep;
                 currentTime <= horizon;
                 currentTime += coarseStep)
            {
                AltAzPoint current = positionAt(currentTime);
                double ceiling = CeilingBoundaryChecker.GetCeilingAltitude(current.Azimuth, boundary);
                double gap = current.Altitude - ceiling;

                if (double.IsFinite(previousGap) && double.IsFinite(gap) &&
                    previousGap < 0.0 && gap >= 0.0)
                {
                    TimeSpan low = previousTime;
                    TimeSpan high = currentTime;

                    // Refine the first crossing to about one second.
                    while ((high - low).TotalSeconds > 1.0)
                    {
                        TimeSpan mid = TimeSpan.FromTicks((low.Ticks + high.Ticks) / 2);
                        AltAzPoint midPosition = positionAt(mid);
                        double midCeiling = CeilingBoundaryChecker.GetCeilingAltitude(
                            midPosition.Azimuth,
                            boundary);
                        double midGap = midPosition.Altitude - midCeiling;

                        if (midGap >= 0.0)
                        {
                            high = mid;
                        }
                        else
                        {
                            low = mid;
                        }
                    }

                    AltAzPoint crossing = positionAt(high);
                    double crossingCeiling = CeilingBoundaryChecker.GetCeilingAltitude(
                        crossing.Azimuth,
                        boundary);

                    return new CeilingPrediction
                    {
                        Available = true,
                        HasCrossing = true,
                        TimeToCrossing = high,
                        CrossingTimeLocal = DateTime.Now + high,
                        CrossingAzimuth = crossing.Azimuth,
                        CrossingAltitude = crossingCeiling,
                        Method = method
                    };
                }

                previousGap = gap;
                previousTime = currentTime;
            }

            return new CeilingPrediction
            {
                Available = true,
                HasCrossing = false,
                Method = method,
                Reason = $"no crossing in the next {horizon.TotalHours:0.#} h"
            };
        }

        private static AltAzPoint EquatorialToHorizontal(
            double rightAscensionHours,
            double declinationDegrees,
            double localSiderealTimeHours,
            double siteLatitudeDegrees)
        {
            double hourAngleDegrees = NormalizeSignedDegrees(
                (localSiderealTimeHours - rightAscensionHours) * 15.0);

            double h = hourAngleDegrees * DegreesToRadians;
            double dec = declinationDegrees * DegreesToRadians;
            double lat = siteLatitudeDegrees * DegreesToRadians;

            double sinAltitude =
                Math.Sin(dec) * Math.Sin(lat) +
                Math.Cos(dec) * Math.Cos(lat) * Math.Cos(h);

            sinAltitude = Math.Clamp(sinAltitude, -1.0, 1.0);
            double altitude = Math.Asin(sinAltitude);

            // Azimuth is measured from north through east, matching ASCOM/N.I.N.A.
            double y = -Math.Sin(h) * Math.Cos(dec);
            double x =
                Math.Sin(dec) * Math.Cos(lat) -
                Math.Cos(dec) * Math.Sin(lat) * Math.Cos(h);

            double azimuth = Math.Atan2(y, x) * RadiansToDegrees;
            azimuth = AltAzPoint.NormalizeAzimuth(azimuth);

            return new AltAzPoint(azimuth, altitude * RadiansToDegrees);
        }

        private static double NormalizeHours(double hours)
        {
            return (hours % 24.0 + 24.0) % 24.0;
        }

        private static double NormalizeSignedDegrees(double degrees)
        {
            double normalized = (degrees % 360.0 + 360.0) % 360.0;
            if (normalized > 180.0) normalized -= 360.0;
            return normalized;
        }
    }

    [Export(typeof(ISequenceCondition))]
    [ExportMetadata("Name", "Altitude Above Ceiling Limit")]
    [ExportMetadata("Description", "Stops further container iterations when telescope coordinates cross above a custom ceiling limit.")]
    [ExportMetadata("Icon", "CeilingLimitSvg")]
    [ExportMetadata("Category", "Loop Condition")]
    [JsonObject(MemberSerialization.OptIn)]
    public class CeilingCondition : SequenceCondition, IValidatable
    {
        private const double DuplicateAzimuthTolerance = 1e-9;
        private const double DuplicateAltitudeTolerance = 1e-6;

        private readonly ITelescopeMediator _telescopeMediator;

        private string _ceilingFilePath = string.Empty;
        private List<AltAzPoint> _cachedPoints = new();
        private DateTime _lastFileWriteUtc = DateTime.MinValue;

        private ITelescope? _cachedFallbackTelescope;
        private bool _fallbackUseLogged;

        private IList<string> _issues = new List<string>();

        private double _predictionHorizonHours = 8.0;
        private bool _enableAdvanceWarning;
        private double _advanceWarningMinutes = 10.0;

        private string _currentPositionText = "Current: unavailable";
        private string _ceilingEstimateText = "Estimated ceiling: not calculated";
        private string _predictionMethodText = string.Empty;

        private MotionSample? _olderMotionSample;
        private MotionSample? _newerMotionSample;
        private DateTime? _advanceWarningForCrossingLocal;

        [JsonIgnore]
        public ICommand BrowseFileCommand { get; }

        [JsonIgnore]
        public ICommand RefreshEstimateCommand { get; }

        public IList<string> Issues
        {
            get => _issues;
            set
            {
                _issues = value;
                RaisePropertyChanged();
            }
        }

        [JsonIgnore]
        public string CurrentPositionText
        {
            get => _currentPositionText;
            private set
            {
                if (_currentPositionText != value)
                {
                    _currentPositionText = value;
                    RaisePropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public string CeilingEstimateText
        {
            get => _ceilingEstimateText;
            private set
            {
                if (_ceilingEstimateText != value)
                {
                    _ceilingEstimateText = value;
                    RaisePropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public string PredictionMethodText
        {
            get => _predictionMethodText;
            private set
            {
                if (_predictionMethodText != value)
                {
                    _predictionMethodText = value;
                    RaisePropertyChanged();
                }
            }
        }

        [ImportingConstructor]
        public CeilingCondition(ITelescopeMediator telescopeMediator)
        {
            _telescopeMediator = telescopeMediator;

            Name = "Altitude Above Ceiling Limit";

            if (Application.Current?.TryFindResource("CeilingLimitSvg") is GeometryGroup geometryGroup)
            {
                Icon = geometryGroup;
            }
            else if (Application.Current?.TryFindResource("CeilingLimitSvg") is Geometry geometry)
            {
                Icon = new GeometryGroup { Children = { geometry } };
            }

            BrowseFileCommand = new DelegateCommand(BrowseFile);
            RefreshEstimateCommand = new DelegateCommand(() => RefreshEstimate(false));
        }

        private CeilingCondition(CeilingCondition cloneMe)
            : this(cloneMe._telescopeMediator)
        {
            CopyMetaData(cloneMe);
            CeilingFilePath = cloneMe.CeilingFilePath;
            PredictionHorizonHours = cloneMe.PredictionHorizonHours;
            EnableAdvanceWarning = cloneMe.EnableAdvanceWarning;
            AdvanceWarningMinutes = cloneMe.AdvanceWarningMinutes;
        }

        [JsonProperty]
        public string CeilingFilePath
        {
            get => _ceilingFilePath;
            set
            {
                string newValue = value ?? string.Empty;

                if (string.Equals(_ceilingFilePath, newValue, StringComparison.Ordinal))
                {
                    return;
                }

                _ceilingFilePath = newValue;
                InvalidateFileCache();
                RaisePropertyChanged();
                Validate();
            }
        }

        [JsonProperty]
        public double PredictionHorizonHours
        {
            get => _predictionHorizonHours;
            set
            {
                if (Math.Abs(_predictionHorizonHours - value) > 1e-9)
                {
                    _predictionHorizonHours = value;
                    RaisePropertyChanged();
                    Validate();
                }
            }
        }

        [JsonProperty]
        public bool EnableAdvanceWarning
        {
            get => _enableAdvanceWarning;
            set
            {
                if (_enableAdvanceWarning != value)
                {
                    _enableAdvanceWarning = value;
                    _advanceWarningForCrossingLocal = null;
                    RaisePropertyChanged();
                }
            }
        }

        [JsonProperty]
        public double AdvanceWarningMinutes
        {
            get => _advanceWarningMinutes;
            set
            {
                if (Math.Abs(_advanceWarningMinutes - value) > 1e-9)
                {
                    _advanceWarningMinutes = value;
                    _advanceWarningForCrossingLocal = null;
                    RaisePropertyChanged();
                    Validate();
                }
            }
        }

        private void BrowseFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Horizon/Ceiling Files (*.hrz;*.txt;*.csv)|*.hrz;*.txt;*.csv|All Files (*.*)|*.*",
                Title = "Select Ceiling Boundary File",
                CheckFileExists = true,
                Multiselect = false
            };

            if (!string.IsNullOrWhiteSpace(CeilingFilePath))
            {
                try
                {
                    string? directory = Path.GetDirectoryName(CeilingFilePath);
                    if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                    {
                        dialog.InitialDirectory = directory;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"CeilingCondition: Could not determine initial file-dialog directory: {ex.Message}");
                }
            }

            if (dialog.ShowDialog() == true)
            {
                CeilingFilePath = dialog.FileName;
                RefreshEstimate(false);
            }
        }

        private void InvalidateFileCache()
        {
            _cachedPoints = new List<AltAzPoint>();
            _lastFileWriteUtc = DateTime.MinValue;
        }

        private List<AltAzPoint> LoadPointsFromFile(out string? error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(CeilingFilePath))
            {
                error = "No ceiling boundary file is selected.";
                return new List<AltAzPoint>();
            }

            if (!File.Exists(CeilingFilePath))
            {
                error = $"Ceiling boundary file does not exist: {CeilingFilePath}";
                return new List<AltAzPoint>();
            }

            try
            {
                var fileInfo = new FileInfo(CeilingFilePath);

                if (fileInfo.LastWriteTimeUtc == _lastFileWriteUtc &&
                    _cachedPoints.Count >= 2)
                {
                    return _cachedPoints;
                }

                var rawPoints = new List<AltAzPoint>();
                string[] lines = File.ReadAllLines(CeilingFilePath);

                for (int lineNumber = 0; lineNumber < lines.Length; lineNumber++)
                {
                    string line = lines[lineNumber].Trim();

                    if (string.IsNullOrEmpty(line) ||
                        line.StartsWith("#", StringComparison.Ordinal) ||
                        line.StartsWith("//", StringComparison.Ordinal) ||
                        line.StartsWith(";", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string[] parts = line.Split(
                        new[] { ' ', '\t', ',', ';' },
                        StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length < 2 ||
                        !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double azimuth) ||
                        !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double altitude) ||
                        !double.IsFinite(azimuth) ||
                        !double.IsFinite(altitude))
                    {
                        Logger.Warning(
                            $"CeilingCondition: Ignoring non-data/malformed line {lineNumber + 1} " +
                            $"in '{CeilingFilePath}': {line}");
                        continue;
                    }

                    if (altitude < -90.0 || altitude > 90.0)
                    {
                        Logger.Warning(
                            $"CeilingCondition: Ignoring line {lineNumber + 1} in '{CeilingFilePath}' " +
                            $"because altitude {altitude}° is outside [-90°, 90°].");
                        continue;
                    }

                    rawPoints.Add(new AltAzPoint(azimuth, altitude));
                }

                List<AltAzPoint> normalized = CollapseDuplicateAzimuths(rawPoints, out error);

                if (error != null)
                {
                    InvalidateFileCache();
                    return new List<AltAzPoint>();
                }

                if (normalized.Count < 2)
                {
                    error =
                        $"Ceiling boundary requires at least two distinct valid azimuth points; " +
                        $"found {normalized.Count}.";
                    InvalidateFileCache();
                    return new List<AltAzPoint>();
                }

                _cachedPoints = normalized;
                _lastFileWriteUtc = fileInfo.LastWriteTimeUtc;

                return _cachedPoints;
            }
            catch (Exception ex)
            {
                error = $"Could not read ceiling boundary file: {ex.Message}";
                Logger.Error($"CeilingCondition: {error}");
                InvalidateFileCache();
                return new List<AltAzPoint>();
            }
        }

        private static List<AltAzPoint> CollapseDuplicateAzimuths(
            IEnumerable<AltAzPoint> points,
            out string? error)
        {
            error = null;

            var sorted = points.OrderBy(p => p.Azimuth).ToList();
            var result = new List<AltAzPoint>();

            foreach (AltAzPoint point in sorted)
            {
                if (result.Count == 0)
                {
                    result.Add(point);
                    continue;
                }

                AltAzPoint previous = result[^1];

                if (Math.Abs(point.Azimuth - previous.Azimuth) <= DuplicateAzimuthTolerance)
                {
                    if (Math.Abs(point.Altitude - previous.Altitude) > DuplicateAltitudeTolerance)
                    {
                        error =
                            $"Conflicting ceiling altitudes are defined at azimuth " +
                            $"{point.Azimuth:F6}° ({previous.Altitude:F3}° and {point.Altitude:F3}°).";
                        return new List<AltAzPoint>();
                    }

                    continue;
                }

                result.Add(point);
            }

            return result;
        }

        private bool TryGetCoordinates(
            out double azimuth,
            out double altitude,
            out string source,
            out TelescopeInfo? telescopeInfo)
        {
            telescopeInfo = null;

            if (TryGetCoordinatesFromMediator(out azimuth, out altitude, out telescopeInfo))
            {
                source = "ITelescopeMediator";
                return true;
            }

            if (TryGetCoordinatesFromUiFallback(out azimuth, out altitude))
            {
                source = "UI/reflection fallback";

                if (!_fallbackUseLogged)
                {
                    Logger.Info(
                        "CeilingCondition: ITelescopeMediator did not provide a connected mount " +
                        "with valid Alt/Az. Using the UI/reflection telescope-discovery fallback.");
                    _fallbackUseLogged = true;
                }

                return true;
            }

            azimuth = 0;
            altitude = 0;
            source = "none";
            return false;
        }

        private bool TryGetCoordinatesFromMediator(
            out double azimuth,
            out double altitude,
            out TelescopeInfo? telescopeInfo)
        {
            azimuth = 0;
            altitude = 0;
            telescopeInfo = null;

            try
            {
                TelescopeInfo info = _telescopeMediator.GetInfo();

                if (info != null &&
                    info.Connected &&
                    double.IsFinite(info.Azimuth) &&
                    double.IsFinite(info.Altitude))
                {
                    azimuth = info.Azimuth;
                    altitude = info.Altitude;
                    telescopeInfo = info;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(
                    $"CeilingCondition: ITelescopeMediator coordinate lookup failed: {ex.Message}");
            }

            return false;
        }

        private bool TryGetCoordinatesFromUiFallback(
            out double azimuth,
            out double altitude)
        {
            azimuth = 0;
            altitude = 0;

            var app = Application.Current;
            if (app == null)
            {
                return false;
            }

            double targetAzimuth = 0;
            double targetAltitude = 0;
            bool retrieved = false;

            try
            {
                Action resolve = () =>
                {
                    if (_cachedFallbackTelescope is { Connected: true } cached &&
                        double.IsFinite(cached.Azimuth) &&
                        double.IsFinite(cached.Altitude))
                    {
                        targetAzimuth = cached.Azimuth;
                        targetAltitude = cached.Altitude;
                        retrieved = true;
                        return;
                    }

                    _cachedFallbackTelescope = null;

                    foreach (Window window in app.Windows.OfType<Window>())
                    {
                        object? dataContext = window.DataContext;
                        if (dataContext == null) continue;

                        ITelescope? telescope = FindConnectedTelescope(dataContext, 0);
                        if (telescope == null || !telescope.Connected) continue;

                        if (!double.IsFinite(telescope.Azimuth) ||
                            !double.IsFinite(telescope.Altitude))
                        {
                            continue;
                        }

                        _cachedFallbackTelescope = telescope;
                        targetAzimuth = telescope.Azimuth;
                        targetAltitude = telescope.Altitude;
                        retrieved = true;
                        return;
                    }
                };

                if (app.Dispatcher.CheckAccess()) resolve();
                else app.Dispatcher.Invoke(resolve);

                if (retrieved)
                {
                    azimuth = targetAzimuth;
                    altitude = targetAltitude;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(
                    $"CeilingCondition: UI/reflection telescope fallback failed: {ex.Message}");
            }

            return false;
        }

        private static ITelescope? FindConnectedTelescope(object? root, int depth)
        {
            if (root == null || depth > 4) return null;

            if (root is ITelescope direct && direct.Connected)
            {
                return direct;
            }

            Type type = root.GetType();

            if (type.IsPrimitive || type == typeof(string) || type.IsValueType)
            {
                return null;
            }

            PropertyInfo[] properties;

            try
            {
                properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            }
            catch
            {
                return null;
            }

            foreach (PropertyInfo property in properties)
            {
                if (property.GetIndexParameters().Length > 0) continue;

                try
                {
                    Type propertyType = property.PropertyType;

                    if (typeof(ITelescope).IsAssignableFrom(propertyType))
                    {
                        if (property.GetValue(root) is ITelescope telescope && telescope.Connected)
                        {
                            return telescope;
                        }
                    }

                    bool looksRelevant =
                        propertyType.Name.Contains("Telescope", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("Telescope", StringComparison.OrdinalIgnoreCase) ||
                        propertyType.Name.Contains("Equipment", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("Equipment", StringComparison.OrdinalIgnoreCase);

                    if (!looksRelevant) continue;

                    object? nestedValue = property.GetValue(root);
                    if (nestedValue == null) continue;

                    ITelescope? nested = FindConnectedTelescope(nestedValue, depth + 1);
                    if (nested is { Connected: true }) return nested;
                }
                catch
                {
                    // Some N.I.N.A. view-model properties can throw when inspected.
                    // Ignore them and continue through telescope/equipment-related nodes.
                }
            }

            return null;
        }

        private void UpdateMotionSamples(double azimuth, double altitude)
        {
            DateTime now = DateTime.UtcNow;
            var sample = new MotionSample(now, azimuth, altitude);

            if (_newerMotionSample == null)
            {
                _newerMotionSample = sample;
                return;
            }

            double elapsed = (now - _newerMotionSample.Value.Utc).TotalSeconds;

            // Ignore N.I.N.A.'s repeated condition evaluations that occur milliseconds apart.
            if (elapsed < 20.0)
            {
                return;
            }

            // If the previous sample is very old, start a fresh pair rather than
            // extrapolating through a likely slew or long interruption.
            if (elapsed > 20.0 * 60.0)
            {
                _olderMotionSample = null;
                _newerMotionSample = sample;
                return;
            }

            _olderMotionSample = _newerMotionSample;
            _newerMotionSample = sample;
        }

        private CeilingPrediction BuildPrediction(
            IReadOnlyList<AltAzPoint> points,
            double currentAzimuth,
            double currentAltitude,
            TelescopeInfo? telescopeInfo)
        {
            double horizonHours = double.IsFinite(PredictionHorizonHours)
                ? Math.Clamp(PredictionHorizonHours, 0.1, 24.0)
                : 8.0;
            TimeSpan horizon = TimeSpan.FromHours(horizonHours);

            if (CeilingBoundaryChecker.IsAboveCeiling(currentAzimuth, currentAltitude, points))
            {
                return new CeilingPrediction
                {
                    Available = true,
                    AlreadyAbove = true,
                    HasCrossing = true,
                    TimeToCrossing = TimeSpan.Zero,
                    CrossingTimeLocal = DateTime.Now,
                    CrossingAzimuth = currentAzimuth,
                    CrossingAltitude = CeilingBoundaryChecker.GetCeilingAltitude(currentAzimuth, points),
                    Method = "current position"
                };
            }

            if (telescopeInfo != null)
            {
                if (telescopeInfo.Slewing)
                {
                    return new CeilingPrediction
                    {
                        Available = false,
                        Reason = "mount is slewing"
                    };
                }

                if (!telescopeInfo.TrackingEnabled)
                {
                    return new CeilingPrediction
                    {
                        Available = false,
                        Reason = "tracking is stopped"
                    };
                }

                string trackingMode = GetTrackingModeName(telescopeInfo);
                bool useFixedEquatorialModel =
                    trackingMode.Equals("Sidereal", StringComparison.OrdinalIgnoreCase) ||
                    trackingMode.Equals("King", StringComparison.OrdinalIgnoreCase) ||
                    trackingMode.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

                if (useFixedEquatorialModel)
                {
                    CeilingPrediction sidereal = CeilingPredictor.PredictFixedEquatorialTrack(
                        currentAzimuth,
                        currentAltitude,
                        telescopeInfo.RightAscension,
                        telescopeInfo.Declination,
                        telescopeInfo.SiderealTime,
                        telescopeInfo.SiteLatitude,
                        points,
                        horizon);

                    if (sidereal.Available)
                    {
                        return sidereal;
                    }
                }
            }

            if (_olderMotionSample.HasValue && _newerMotionSample.HasValue)
            {
                return CeilingPredictor.PredictLinearMotion(
                    _olderMotionSample.Value,
                    _newerMotionSample.Value,
                    points,
                    TimeSpan.FromHours(Math.Min(horizonHours, 2.0)));
            }

            string modeName = telescopeInfo == null ? "unknown" : GetTrackingModeName(telescopeInfo);
            return new CeilingPrediction
            {
                Available = false,
                Reason =
                    $"tracking mode {modeName}; need another position sample for motion extrapolation"
            };
        }

        private static string GetTrackingModeName(TelescopeInfo telescopeInfo)
        {
            try
            {
                return telescopeInfo.TrackingRate.TrackingMode.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }

        private void UpdatePredictionDisplay(
            IReadOnlyList<AltAzPoint> points,
            double currentAzimuth,
            double currentAltitude,
            string coordinateSource,
            TelescopeInfo? telescopeInfo,
            bool allowAdvanceWarning)
        {
            double currentCeiling = CeilingBoundaryChecker.GetCeilingAltitude(
                currentAzimuth,
                points);

            CurrentPositionText =
                $"Current: Az {currentAzimuth:F1}°   Alt {currentAltitude:F1}°   " +
                $"Ceiling {currentCeiling:F1}°";

            UpdateMotionSamples(currentAzimuth, currentAltitude);

            CeilingPrediction prediction = BuildPrediction(
                points,
                currentAzimuth,
                currentAltitude,
                telescopeInfo);

            if (!prediction.Available)
            {
                CeilingEstimateText = $"Estimated ceiling: unavailable — {prediction.Reason}";
                PredictionMethodText = $"Source: {coordinateSource}";
                _advanceWarningForCrossingLocal = null;
                return;
            }

            if (prediction.AlreadyAbove)
            {
                CeilingEstimateText = "Estimated ceiling: reached";
                PredictionMethodText = $"Source: {coordinateSource}";
                return;
            }

            if (!prediction.HasCrossing)
            {
                CeilingEstimateText = $"Estimated ceiling: {prediction.Reason}";
                PredictionMethodText = $"Model: {prediction.Method}; source: {coordinateSource}";
                _advanceWarningForCrossingLocal = null;
                return;
            }

            CeilingEstimateText =
                $"Estimated ceiling: {FormatDuration(prediction.TimeToCrossing)} " +
                $"({prediction.CrossingTimeLocal:HH:mm:ss})   " +
                $"Az {prediction.CrossingAzimuth:F1}° / Alt {prediction.CrossingAltitude:F1}°";

            string trackingMode = telescopeInfo == null ? "unknown" : GetTrackingModeName(telescopeInfo);
            PredictionMethodText =
                $"Model: {prediction.Method}; tracking: {trackingMode}; source: {coordinateSource}";

            Logger.Debug(
                $"CeilingCondition estimate: ETA={prediction.TimeToCrossing}, " +
                $"Crossing={prediction.CrossingTimeLocal:yyyy-MM-dd HH:mm:ss}, " +
                $"Az={prediction.CrossingAzimuth:F2}°, Alt={prediction.CrossingAltitude:F2}°, " +
                $"Method={prediction.Method}, Tracking={trackingMode}");

            if (allowAdvanceWarning)
            {
                MaybeShowAdvanceWarning(prediction);
            }
        }

        private void MaybeShowAdvanceWarning(CeilingPrediction prediction)
        {
            if (!EnableAdvanceWarning ||
                !prediction.Available ||
                !prediction.HasCrossing ||
                prediction.AlreadyAbove ||
                !double.IsFinite(AdvanceWarningMinutes) ||
                AdvanceWarningMinutes <= 0.0)
            {
                return;
            }

            TimeSpan warningLead = TimeSpan.FromMinutes(AdvanceWarningMinutes);

            if (prediction.TimeToCrossing <= TimeSpan.Zero ||
                prediction.TimeToCrossing > warningLead)
            {
                if (prediction.TimeToCrossing > warningLead + TimeSpan.FromMinutes(2))
                {
                    _advanceWarningForCrossingLocal = null;
                }
                return;
            }

            bool alreadyWarnedForThisCrossing =
                _advanceWarningForCrossingLocal.HasValue &&
                Math.Abs(
                    (_advanceWarningForCrossingLocal.Value - prediction.CrossingTimeLocal)
                    .TotalMinutes) < 2.0;

            if (alreadyWarnedForThisCrossing)
            {
                return;
            }

            string message =
                $"Ceiling expected in {FormatDuration(prediction.TimeToCrossing)} " +
                $"at {prediction.CrossingTimeLocal:HH:mm:ss} " +
                $"(Az {prediction.CrossingAzimuth:F1}°, Alt {prediction.CrossingAltitude:F1}°).";

            Logger.Info($"CeilingCondition: {message}");
            Notification.ShowWarning(message);
            _advanceWarningForCrossingLocal = prediction.CrossingTimeLocal;
        }

        private void RefreshEstimate(bool allowAdvanceWarning)
        {
            List<AltAzPoint> points = LoadPointsFromFile(out string? fileError);

            if (fileError != null || points.Count < 2)
            {
                CurrentPositionText = "Current: unavailable";
                CeilingEstimateText =
                    $"Estimated ceiling: unavailable — {fileError ?? "invalid boundary"}";
                PredictionMethodText = string.Empty;
                return;
            }

            if (!TryGetCoordinates(
                    out double currentAzimuth,
                    out double currentAltitude,
                    out string coordinateSource,
                    out TelescopeInfo? telescopeInfo))
            {
                CurrentPositionText = "Current: telescope unavailable";
                CeilingEstimateText = "Estimated ceiling: unavailable — telescope coordinates not resolved";
                PredictionMethodText = string.Empty;
                return;
            }

            UpdatePredictionDisplay(
                points,
                currentAzimuth,
                currentAltitude,
                coordinateSource,
                telescopeInfo,
                allowAdvanceWarning);
        }

        private static string FormatDuration(TimeSpan time)
        {
            if (time.TotalHours >= 1.0)
            {
                int hours = (int)Math.Floor(time.TotalHours);
                return $"{hours:00}:{time.Minutes:00}:{time.Seconds:00}";
            }

            return $"{time.Minutes:00}:{time.Seconds:00}";
        }

        public bool Validate()
        {
            var validationIssues = new List<string>();

            List<AltAzPoint> points = LoadPointsFromFile(out string? error);

            if (error != null)
            {
                validationIssues.Add(error);
            }
            else if (points.Count < 2)
            {
                validationIssues.Add(
                    "Ceiling boundary requires at least two distinct valid points.");
            }

            if (!double.IsFinite(PredictionHorizonHours) ||
                PredictionHorizonHours < 0.1 ||
                PredictionHorizonHours > 24.0)
            {
                validationIssues.Add(
                    "Prediction horizon must be between 0.1 and 24 hours.");
            }

            if (!double.IsFinite(AdvanceWarningMinutes) ||
                AdvanceWarningMinutes < 0.0 ||
                AdvanceWarningMinutes > 1440.0)
            {
                validationIssues.Add(
                    "Advance warning must be between 0 and 1440 minutes.");
            }

            Issues = validationIssues;
            return validationIssues.Count == 0;
        }

        public override void AfterParentChanged()
        {
            base.AfterParentChanged();
            Validate();
            RefreshEstimate(false);
        }

        public override bool Check(ISequenceItem context, ISequenceItem item)
        {
            List<AltAzPoint> points = LoadPointsFromFile(out string? fileError);

            if (fileError != null || points.Count < 2)
            {
                Logger.Warning(
                    $"CeilingCondition: Boundary unavailable during check: " +
                    $"{fileError ?? "fewer than two valid points"}. Returning true.");
                return true;
            }

            if (!TryGetCoordinates(
                    out double currentAzimuth,
                    out double currentAltitude,
                    out string coordinateSource,
                    out TelescopeInfo? telescopeInfo))
            {
                Logger.Warning(
                    "CeilingCondition: Active telescope coordinates could not be resolved " +
                    "from either ITelescopeMediator or the UI fallback. Returning true.");
                return true;
            }

            double ceilingAltitude =
                CeilingBoundaryChecker.GetCeilingAltitude(currentAzimuth, points);

            bool isAbove =
                double.IsFinite(ceilingAltitude) &&
                currentAltitude > ceilingAltitude;

            Logger.Debug(
                $"CeilingCondition check: Source={coordinateSource}, " +
                $"Az={currentAzimuth:F2}°, Alt={currentAltitude:F2}°, " +
                $"Ceiling={ceilingAltitude:F2}°, Points={points.Count}, " +
                $"IsAboveCeiling={isAbove}");

            UpdatePredictionDisplay(
                points,
                currentAzimuth,
                currentAltitude,
                coordinateSource,
                telescopeInfo,
                allowAdvanceWarning: true);

            if (isAbove)
            {
                string warningMessage =
                    $"Ceiling Limit Exceeded: Alt ({currentAltitude:F1}°) " +
                    $"exceeded ceiling ({ceilingAltitude:F1}°) at " +
                    $"Az ({currentAzimuth:F1}°). Stopping container.";

                Logger.Warning($"CeilingCondition: {warningMessage}");
                Notification.ShowWarning(warningMessage);
                return false;
            }

            return true;
        }

        public override object Clone()
        {
            return new CeilingCondition(this);
        }

        public override string ToString()
        {
            return
                $"Category: {Category}, Item: {nameof(CeilingCondition)}, " +
                $"File: {CeilingFilePath}";
        }

        private sealed class DelegateCommand : ICommand
        {
            private readonly Action _execute;

            public DelegateCommand(Action execute)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            }

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter) => _execute();

            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }
        }
    }
}
