using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("Ceiling")]
[assembly: AssemblyDescription("Stops sequence imaging when telescope coordinates cross above a custom ceiling/horizon boundary.")]
[assembly: AssemblyCompany("Jan Kalin")]
[assembly: AssemblyProduct("Ceiling")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Jan Kalin")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

// Keep this GUID unchanged for all future versions of the plugin.
[assembly: Guid("e5f92a18-4b71-460b-8d77-a123bc89d9e4")]

[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

[assembly: AssemblyMetadata("ShortDescription", "Stops imaging when altitude exceeds a custom ceiling boundary.")]
[assembly: AssemblyMetadata("LongDescription", "Monitors telescope Alt/Az coordinates against an imported polyline/horizon file and stops further iterations of the sequence container when the ceiling limit is exceeded.")]
[assembly: AssemblyMetadata("License", "MPL-2.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
[assembly: AssemblyMetadata("Repository", "https://github.com/JanKalin/AstroRepo/Nina/Plugins/Ceiling")]
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.2.0.9001")]
