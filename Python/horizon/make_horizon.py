#!/usr/bin/env python3
import argparse
import math
import sys
import time
from pathlib import Path
import win32com.client


def parse_args():
    parser = argparse.ArgumentParser(
        description="Convert Az/Alt to RA/Dec and slew an ASCOM mount in EQ mode with tracking disabled."
    )
    parser.add_argument(
        "--az",
        type=float,
        default=None,
        help="Target Azimuth in degrees (0=N, 90=E, 180=S, 270=W)",
    )
    parser.add_argument(
        "--alt",
        type=float,
        default=None,
        help="Target Altitude in degrees above horizon",
    )
    parser.add_argument(
        "--driver",
        type=str,
        default="ASCOM.ASIMount.Telescope",
        help="ASCOM ProgID (default: ASCOM.ASIMount.Telescope)",
    )
    parser.add_argument(
        "--step",
        type=float,
        default=2.0,
        help="Default nudge step size in degrees for u/d/l/r (default: 2.0)",
    )
    parser.add_argument(
        "--test",
        action="store_true",
        help="Dry run: simulate calculations without moving the mount",
    )
    parser.add_argument(
        "-i", "--interactive",
        action="store_true",
        help="Launch interactive terminal mode",
    )
    return parser.parse_args()


def disable_tracking(scope, test_mode: bool = False):
    """Ensures tracking is unconditionally stopped."""
    if test_mode or scope is None:
        return

    try:
        if scope.Tracking:
            scope.Tracking = False
            print("[Mount] Tracking stopped.")
    except Exception as e:
        print(f"[ASCOM Error] Could not disable tracking: {e}", file=sys.stderr)


def azalt_to_mount_radec(scope, az_deg: float, alt_deg: float):
    """
    Computes RA/Dec using standard spherical trigonometry and the mount's
    own reported SiteLatitude and SiderealTime (LST).
    """
    default_lat = 46.074444
    lat_deg = default_lat

    # Fetch Latitude safely
    if scope is not None:
        try:
            val = float(scope.SiteLatitude)
            if abs(val) > 0.001:
                lat_deg = val
        except Exception:
            lat_deg = default_lat

    lat_rad = math.radians(lat_deg)
    alt_rad = math.radians(alt_deg)
    az_rad = math.radians(az_deg)

    # 1. Declination: sin(Dec) = sin(Lat)*sin(Alt) + cos(Lat)*cos(Alt)*cos(Az)
    sin_dec = math.sin(lat_rad) * math.sin(alt_rad) + math.cos(lat_rad) * math.cos(alt_rad) * math.cos(az_rad)
    sin_dec = max(-1.0, min(1.0, sin_dec))
    dec_rad = math.asin(sin_dec)
    dec_deg = math.degrees(dec_rad)

    # Workaround for ZWO driver bug at exact 0.0 Dec
    if abs(dec_deg) < 1e-5:
        dec_deg = 1e-5

    # 2. Hour Angle (HA): cos(HA) = (sin(Alt) - sin(Lat)*sin(Dec)) / (cos(Lat)*cos(Dec))
    cos_dec = math.cos(math.radians(dec_deg))
    if abs(cos_dec) < 1e-7:
        ha_rad = 0.0
    else:
        cos_ha = (math.sin(alt_rad) - math.sin(lat_rad) * math.sin(math.radians(dec_deg))) / (math.cos(lat_rad) * cos_dec)
        cos_ha = max(-1.0, min(1.0, cos_ha))
        ha_rad = math.acos(cos_ha)

    # Azimuth: 0..180 (East of meridian) has negative HA / HA > 12h
    if math.sin(az_rad) > 0:
        ha_rad = 2 * math.pi - ha_rad

    ha_hours = math.degrees(ha_rad) / 15.0

    # 3. RA = LST - HA
    lst_hours = 0.0
    if scope is not None:
        try:
            lst_hours = float(scope.SiderealTime)
        except Exception:
            pass

    ra_hours = (lst_hours - ha_hours) % 24.0
    return ra_hours, dec_deg, lst_hours, lat_deg


def slew_to_azalt(scope, az_deg: float, alt_deg: float, test_mode: bool = False) -> bool:
    """Slews the mount using the mount's internal LST reference and stops tracking."""
    # Normalize Azimuth to [0, 360) and clamp Altitude to [-90, 90]
    az_deg = az_deg % 360.0
    alt_deg = max(-90.0, min(90.0, alt_deg))

    ra_hours, dec_deg, lst_hours, lat_deg = azalt_to_mount_radec(scope, az_deg, alt_deg)

    print(f"\n[Target]      Az: {az_deg:6.2f}° | Alt: {alt_deg:5.2f}°")
    print(f"[Mount Sync]  LST: {lst_hours:7.4f} h | Lat: {lat_deg:+7.4f}°")
    print(f"[Equatorial]  RA: {ra_hours:8.5f} h | Dec: {dec_deg:+9.5f}°")

    if alt_deg < 0:
        print("[Warning] Target altitude is below the horizon (alt < 0°).")

    if test_mode:
        print("[TEST MODE] Slew command simulated (mount movement skipped).")
        return True

    try:
        print(f"Initiating slew to RA: {ra_hours:.4f}h, Dec: {dec_deg:+.4f}°...")
        scope.SlewToCoordinatesAsync(ra_hours, dec_deg)

        while scope.Slewing:
            print(f"  Slewing... RA: {scope.RightAscension:7.4f}h | Dec: {scope.Declination:+7.4f}°", end="\r")
            time.sleep(0.1)

        # Force tracking off immediately upon arrival
        disable_tracking(scope, test_mode=test_mode)

        print(f"\n[Complete] Reached -> RA: {scope.RightAscension:.4f}h, Dec: {scope.Declination:+.4f}° (Az: {scope.Azimuth:.2f}°, Alt: {scope.Altitude:.2f}°)")
        return True
    except Exception as e:
        print(f"[ASCOM Error] Slew failed: {e}", file=sys.stderr)
        return False


def save_to_horizon_file(filename_stem: str, az: float, alt: float):
    """Appends an Az/Alt pair to a N.I.N.A. standard .hrz file (format: Azimuth Altitude)."""
    clean_stem = filename_stem.strip().replace(".hrz", "")
    filepath = Path(f"{clean_stem}.hrz")

    entry = f"{az:.2f} {alt:.2f}\n"
    with open(filepath, "a", encoding="utf-8") as f:
        f.write(entry)

    print(f"[Horizon Saved] Added '{az:.2f} {alt:.2f}' (Az Alt) to '{filepath.resolve()}'")


def get_current_or_last_coords(scope, last_az, last_alt, test_mode: bool):
    """Returns the most recent Az/Alt target or queries live mount position."""
    if last_az is not None and last_alt is not None:
        return last_az, last_alt

    if not test_mode and scope is not None:
        try:
            az = float(scope.Azimuth)
            alt = float(scope.Altitude)
            return az, alt
        except Exception:
            pass
    return 0.0, 0.0


def interactive_loop(scope, default_step: float, test_mode: bool):
    print("\n" + "=" * 65)
    print(" Interactive Az/Alt Slew & Horizon Utility [Tracking DISABLED]")
    if scope is not None and not test_mode:
        try:
            print(f" Site Location : Lat {float(scope.SiteLatitude):+.4f}°, Lon {float(scope.SiteLongitude):+.4f}°, LST {float(scope.SiderealTime):.2f}h")
        except Exception:
            pass
    print(" Commands:")
    print("   <az> <alt>     : Slew to absolute Azimuth and Altitude (e.g. '180 45')")
    print("   u / d          : Up / Down Altitude (1x step: {0:.1f}°)".format(default_step))
    print("   l / r          : Left / Right Azimuth (1x step: {0:.1f}°)".format(default_step))
    print("   U / D          : Up / Down Altitude (5x step: {0:.1f}°)".format(default_step * 5.0))
    print("   L / R          : Left / Right Azimuth (5x step: {0:.1f}°)".format(default_step * 5.0))
    print("   step <deg>     : Set default base step size")
    print("   <filename>     : Append last Az/Alt to '<filename>.hrz'")
    print("   q / quit       : Exit")
    print("=" * 65 + "\n")

    last_az = None
    last_alt = None
    step_deg = default_step

    while True:
        try:
            raw = input("AM5N> ").strip()
        except (KeyboardInterrupt, EOFError):
            print("\nExiting.")
            break

        if not raw:
            continue

        if raw.lower() in ("q", "quit", "exit"):
            print("Exiting.")
            break

        tokens = raw.split()
        first_token = tokens[0]

        # Command: Change step size
        if first_token.lower() == "step" and len(tokens) == 2:
            try:
                step_deg = float(tokens[1])
                print(f"Default step size set to {step_deg:.2f}° (5x step is {step_deg * 5.0:.2f}°)")
                continue
            except ValueError:
                pass

        # Command: U / D / L / R (case-sensitive step multiplier)
        if first_token in ("u", "d", "l", "r", "U", "D", "L", "R"):
            current_az, current_alt = get_current_or_last_coords(scope, last_az, last_alt, test_mode)

            # Uppercase gets 5x multiplier, lowercase gets 1x multiplier
            multiplier = 5.0 if first_token.isupper() else 1.0

            if len(tokens) >= 2:
                try:
                    nudge_amount = float(tokens[1])
                except ValueError:
                    nudge_amount = step_deg * multiplier
            else:
                nudge_amount = step_deg * multiplier

            target_az = current_az
            target_alt = current_alt
            action = first_token.lower()

            if action == "u":
                target_alt += nudge_amount
            elif action == "d":
                target_alt -= nudge_amount
            elif action == "l":
                target_az -= nudge_amount
            elif action == "r":
                target_az += nudge_amount

            success = slew_to_azalt(scope, target_az, target_alt, test_mode=test_mode)
            if success:
                last_az = target_az % 360.0
                last_alt = max(-90.0, min(90.0, target_alt))
            continue

        # Case 1: Two numbers provided -> Absolute Az Alt
        if len(tokens) == 2:
            try:
                az_val = float(tokens[0])
                alt_val = float(tokens[1])

                success = slew_to_azalt(scope, az_val, alt_val, test_mode=test_mode)
                if success:
                    last_az = az_val
                    last_alt = alt_val
                continue
            except ValueError:
                pass

        # Case 2: Single string provided -> Save to .hrz file
        if len(tokens) == 1:
            filename = tokens[0]
            if last_az is None or last_alt is None:
                if not test_mode and scope is not None:
                    try:
                        last_az = float(scope.Azimuth)
                        last_alt = float(scope.Altitude)
                        print(f"Using current mount coordinates: Az={last_az:.2f}°, Alt={last_alt:.2f}°")
                    except Exception:
                        print("Error: No coordinates available to save.", file=sys.stderr)
                        continue
                else:
                    print("Error: No previous Az/Alt coordinate exists. Slew somewhere first.", file=sys.stderr)
                    continue

            save_to_horizon_file(filename, last_az, last_alt)
            continue

        print("Unrecognized input. Commands: '<az> <alt>', 'u'/'d'/'l'/'r' (1x), 'U'/'D'/'L'/'R' (5x), 'step <deg>', '<filename>', or 'q'.")


def main():
    args = parse_args()

    scope = None
    try:
        scope = win32com.client.Dispatch(args.driver)
        if not scope.Connected:
            scope.Connected = True

        disable_tracking(scope, test_mode=args.test)

        print(f"Connected to: {scope.Name}")
        print(f"Current Position -> Az: {scope.Azimuth:.2f}°, Alt: {scope.Altitude:.2f}° (Tracking: {scope.Tracking})")
        print(f"Mount Site       -> Lat: {float(scope.SiteLatitude):+.4f}°, Lon: {float(scope.SiteLongitude):+.4f}°, LST: {float(scope.SiderealTime):.4f}h")
    except Exception as e:
        print(f"Warning: ASCOM connection failed: {e}", file=sys.stderr)
        if not args.test:
            sys.exit(1)
        print("Continuing under --test mode without live ASCOM connection...")

    # Single-shot command line execution
    if args.az is not None and args.alt is not None:
        slew_to_azalt(scope, args.az, args.alt, test_mode=args.test)
        if not args.interactive:
            return

    # Enter interactive shell
    if args.interactive or (args.az is None and args.alt is None):
        interactive_loop(scope, default_step=args.step, test_mode=args.test)


if __name__ == "__main__":
    main()
