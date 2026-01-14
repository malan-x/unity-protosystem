// Packages/com.protosystem.core/Editor/Publishing/Platforms/Epic/EpicPublisher.cs
using System;
using System.Threading.Tasks;

namespace ProtoSystem.Publishing.Editor
{
    /// <summary>
    /// Издатель для Epic Games Store (заглушка)
    /// </summary>
    public class EpicPublisher : IPlatformPublisher
    {
        private readonly EpicConfig _config;

        public string PlatformId => "epic";
        public string DisplayName => "Epic Games Store";
        public bool IsSupported => false; // TODO: Реализовать

        public EpicPublisher(EpicConfig config)
        {
            _config = config;
        }

        public bool ValidateConfig(out string error)
        {
            error = "Epic Games Store integration not yet implemented";
            return false;
        }

        public Task<PublishResult> UploadAsync(string buildPath, string branch, string description,
            IProgress<PublishProgress> progress = null)
        {
            return Task.FromResult(PublishResult.Fail("Epic Games Store integration not yet implemented"));
        }

        public Task<PublishResult> PublishNewsAsync(PatchNotesEntry entry,
            IProgress<PublishProgress> progress = null)
        {
            return Task.FromResult(PublishResult.Fail("Epic Games Store integration not yet implemented"));
        }

        public Task<PublishResult> SetLiveAsync(string buildId, string branch,
            IProgress<PublishProgress> progress = null)
        {
            return Task.FromResult(PublishResult.Fail("Epic Games Store integration not yet implemented"));
        }
    }
}
