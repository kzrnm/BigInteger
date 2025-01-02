using System.Runtime.CompilerServices;

namespace Kzrnm.Numerics.Test
{
    // The collection definitions must be in the same assembly as the test that uses them.
    // So please use "Compile Include" in the project file to include this class.
    [CollectionDefinition(nameof(DisableParallelization), DisableParallelization = true)]
    public class DisableParallelization { }
    public abstract class ThresholdTestsBase
    {
        protected static void RunWithFakeThreshold(in int field, int value, Action action)
        {
            int lastValue = field;

            // This is tricky hack. If only DEBUG build is targeted,
            // `RunWithFakeThreshold(ref int field, int value, Action action action)` would be more appropriate.
            // However, in RELEASE build, the code should be like this.
            // This is because const fields cannot be passed as ref arguments.
            // When a const field is passed to the in argument, a local
            // variable reference is implicitly passed to the in argument
            // so that the original const field value is never rewritten.
            ref int reference = ref Unsafe.AsRef(in field);

            try
            {
                reference = value;
                if (field != value)
                    return; // In release build, the test will be skipped.
                action();
            }
            finally
            {
                reference = lastValue;
            }
        }
    }
}
