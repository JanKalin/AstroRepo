using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("Ceiling")]
[assembly: AssemblyDescription("Stops sequence imaging when telescope coordinates cross above a custom ceiling/horizon boundary and estimates the next crossing time.")]
[assembly: AssemblyCompany("Jan Kalin")]
[assembly: AssemblyProduct("Ceiling")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Jan Kalin")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]
[assembly: Guid("e5f92a18-4b71-460b-8d77-a123bc89d9e4")]

[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

[assembly: AssemblyMetadata("ShortDescription", "Stops imaging at a custom Alt/Az ceiling and estimates time to the next ceiling crossing.")]
[assembly: AssemblyMetadata("LongDescription", "Monitors telescope Alt/Az coordinates against an imported ceiling boundary, estimates the next crossing from current tracking information, and stops further sequence-container iterations when the ceiling is exceeded.")]
[assembly: AssemblyMetadata("License", "MPL-2.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
[assembly: AssemblyMetadata("Repository", "https://github.com/JanKalin/AstroRepo/Nina/Plugins/Ceiling")]
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.2.0.9001")]
