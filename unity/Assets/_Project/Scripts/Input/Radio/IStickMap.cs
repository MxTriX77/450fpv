using Fpv450.Sim.Control;

namespace Fpv450.Input.Radio
{
    /// <summary>
    /// The map from one raw report to a StickSample, run on the input thread for every report: RawStickMap without a
    /// calibration, CalibratedStickMap with the pilot's. It must not allocate, and must not change once in use.
    /// </summary>
    public interface IStickMap
    {
        void Map(RadioReport report, RadioAxis[] axes, out StickSample s);
    }
}
