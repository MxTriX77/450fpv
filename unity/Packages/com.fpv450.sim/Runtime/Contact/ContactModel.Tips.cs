using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Contact
{
    public sealed partial class ContactModel
    {
        // A corner's leg tip in soil (w17-four-legs).
        struct Tip
        {
            public bool Known, Engaged;
        }

        // A tip leaves its soil.
        void LeaveTip(int c)
        {
            _tips[c].Engaged = false;
        }
    }
}
