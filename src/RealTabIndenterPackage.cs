using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace RealTabIndenter
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(RealTabIndenterPackage.PackageGuidString)]
    public sealed class RealTabIndenterPackage : AsyncPackage
    {
        public const string PackageGuidString = "9b8f05b9-7b7a-4273-8f6a-fcada956bec3";

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);
        }
    }
}
