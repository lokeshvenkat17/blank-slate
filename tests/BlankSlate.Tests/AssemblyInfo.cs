using Xunit;

// AvaloniaEdit's TextDocument (and the headless Avalonia app) are thread-affine, so
// running test classes concurrently produces sporadic, order-dependent failures.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
