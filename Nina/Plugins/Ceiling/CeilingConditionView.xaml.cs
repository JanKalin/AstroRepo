using System.ComponentModel.Composition;
using System.Windows;

namespace Ceiling
{
    /// <summary>
    /// MEF-exported resource dictionary for the sequencer condition UI and icon.
    /// N.I.N.A. imports exported ResourceDictionary instances automatically.
    /// </summary>
    [Export(typeof(ResourceDictionary))]
    public partial class CeilingConditionView : ResourceDictionary
    {
        public CeilingConditionView()
        {
            InitializeComponent();
        }
    }
}
