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
    }

    public static class CeilingBoundaryChecker
    {
        /// <summary>
        /// Returns the linearly interpolated ceiling altitude at a given azimuth.
        /// Points are expected to contain at least two distinct azimuths.
        /// The segment crossing 360/0 degrees is handled explicitly.
        /// </summary>
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
            if (azSpan < 0)
            {
                azSpan += 360.0;
            }

            double azOffset = currentAz - p1.Azimuth;
            if (azOffset < 0)
            {
                azOffset += 360.0;
            }

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

        // The fallback is retained deliberately because some setups have previously
        // reported a connected telescope through the UI context while the mediator
        // did not provide usable Alt/Az data.
        private ITelescope? _cachedFallbackTelescope;
        private bool _fallbackUseLogged;

        private IList<string> _issues = new List<string>();

        [JsonIgnore]
        public ICommand BrowseFileCommand { get; }

        public IList<string> Issues
        {
            get => _issues;
            set
            {
                _issues = value;
                RaisePropertyChanged();
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
        }

        private CeilingCondition(CeilingCondition cloneMe)
            : this(cloneMe._telescopeMediator)
        {
            CopyMetaData(cloneMe);
            CeilingFilePath = cloneMe.CeilingFilePath;
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
            }
        }

        private void InvalidateFileCache()
        {
            _cachedPoints = new List<AltAzPoint>();
            _lastFileWriteUtc = DateTime.MinValue;
        }

        /// <summary>
        /// Loads and normalizes the ceiling polyline.
        ///
        /// Parsing remains deliberately permissive to preserve compatibility with
        /// horizon files that contain headers or extra non-data lines. Such lines
        /// are logged and skipped. Duplicate 0/360 entries with the same altitude
        /// are collapsed. Conflicting duplicate azimuths invalidate the boundary.
        /// </summary>
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

            var sorted = points
                .OrderBy(p => p.Azimuth)
                .ToList();

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

                    // Same azimuth and effectively the same altitude, e.g. 0° and 360°.
                    // Keep one copy.
                    continue;
                }

                result.Add(point);
            }

            return result;
        }

        private bool TryGetCoordinates(
            out double azimuth,
            out double altitude,
            out string source)
        {
            if (TryGetCoordinatesFromMediator(out azimuth, out altitude))
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

            source = "none";
            return false;
        }

        private bool TryGetCoordinatesFromMediator(
            out double azimuth,
            out double altitude)
        {
            azimuth = 0;
            altitude = 0;

            try
            {
                var telescopeInfo = _telescopeMediator.GetInfo();

                if (telescopeInfo != null &&
                    telescopeInfo.Connected &&
                    double.IsFinite(telescopeInfo.Azimuth) &&
                    double.IsFinite(telescopeInfo.Altitude))
                {
                    azimuth = telescopeInfo.Azimuth;
                    altitude = telescopeInfo.Altitude;
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

        /// <summary>
        /// Compatibility fallback retained from the working v1.0 implementation.
        /// It is only used when ITelescopeMediator cannot provide valid coordinates.
        /// </summary>
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
                    // Reuse the previously resolved telescope while it remains connected.
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
                        if (dataContext == null)
                        {
                            continue;
                        }

                        ITelescope? telescope = FindConnectedTelescope(dataContext, 0);
                        if (telescope == null || !telescope.Connected)
                        {
                            continue;
                        }

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

                if (app.Dispatcher.CheckAccess())
                {
                    resolve();
                }
                else
                {
                    app.Dispatcher.Invoke(resolve);
                }

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
            if (root == null || depth > 4)
            {
                return null;
            }

            if (root is ITelescope direct && direct.Connected)
            {
                return direct;
            }

            Type type = root.GetType();

            if (type.IsPrimitive ||
                type == typeof(string) ||
                type.IsValueType)
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
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                try
                {
                    Type propertyType = property.PropertyType;

                    if (typeof(ITelescope).IsAssignableFrom(propertyType))
                    {
                        if (property.GetValue(root) is ITelescope telescope &&
                            telescope.Connected)
                        {
                            return telescope;
                        }
                    }

                    bool looksRelevant =
                        propertyType.Name.Contains("Telescope", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("Telescope", StringComparison.OrdinalIgnoreCase) ||
                        propertyType.Name.Contains("Equipment", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("Equipment", StringComparison.OrdinalIgnoreCase);

                    if (!looksRelevant)
                    {
                        continue;
                    }

                    object? nestedValue = property.GetValue(root);
                    if (nestedValue == null)
                    {
                        continue;
                    }

                    ITelescope? nested = FindConnectedTelescope(nestedValue, depth + 1);
                    if (nested is { Connected: true })
                    {
                        return nested;
                    }
                }
                catch
                {
                    // Some view-model properties may throw when read. Ignore those
                    // and keep walking the small telescope/equipment-related subset.
                }
            }

            return null;
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

            // Deliberately do NOT require a connected telescope here.
            // This condition is a convenience imaging limit, not a safety interlock,
            // and sequences may be edited/loaded before the mount is connected.

            Issues = validationIssues;
            return validationIssues.Count == 0;
        }

        public override void AfterParentChanged()
        {
            base.AfterParentChanged();
            Validate();
        }

        public override bool Check(ISequenceItem context, ISequenceItem item)
        {
            // RunCheck() calls Validate() before Check(), but keep this robust when
            // Check() is called directly by tests or future code.
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
                    out string coordinateSource))
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

            Logger.Info(
                $"CeilingCondition check: Source={coordinateSource}, " +
                $"Az={currentAzimuth:F2}°, Alt={currentAltitude:F2}°, " +
                $"Ceiling={ceilingAltitude:F2}°, Points={points.Count}, " +
                $"IsAboveCeiling={isAbove}");

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

            public bool CanExecute(object? parameter)
            {
                return true;
            }

            public void Execute(object? parameter)
            {
                _execute();
            }

            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }
        }
    }
}
