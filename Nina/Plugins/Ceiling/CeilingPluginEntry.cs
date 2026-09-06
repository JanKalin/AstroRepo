using System.ComponentModel.Composition;
using System.Threading.Tasks;
using NINA.Plugin;
using NINA.Plugin.Interfaces;

namespace Ceiling
{
    [Export(typeof(IPluginManifest))]
    public class CeilingPluginEntry : PluginBase
    {
        [ImportingConstructor]
        public CeilingPluginEntry()
        {
        }

        public override Task Teardown()
        {
            return Task.CompletedTask;
        }
    }
}
