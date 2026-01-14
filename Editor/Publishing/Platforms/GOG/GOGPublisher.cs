// Packages/com.protosystem.core/Editor/Publishing/Platforms/GOG/GOGPublisher.cs
using System;
using System.Threading.Tasks;

namespace ProtoSystem.Publishing.Editor
{
    /// <summary>
    /// Издатель для GOG Galaxy (заглушка)
    /// </summary>
    public class GOGPublisher : IPlatformPublisher
    {
        private readonly GOGConfig _config;

        public string PlatformId => "gog";
        public string DisplayName => "GOG Galaxy";
        public bool IsSupported => false; // TODO: Реализовать

        public GOGPublisher(GOGConfig config)
        {
            _config = config;
        }

        public bool ValidateConfig(out string error)
        {
            error = "GOG Galaxy integration not yet implemented";
            return false;
        }

        public Task<PublishResult> UploadAsync(string buildPath, string branch, string description,
            IProgress<PublishProgress> progress = null)
        {
            return Task.FromResult(PublishResult.Fail("GOG Galaxy integration not yet implemented"));
        }

        public Task<PublishResult> PublishNewsAsync(PatchNotesEntry entry,
            IProgress<PublishProgress> progress = null)
        {
            return Task.FromResult(PublishResult.Fail("GOG Galaxy integration not yet implemented"));
        }

        public Task<PublishResult> SetLiveAsync(string buildId, string branch,
            IProgress<PublishProgress> progress = null)
        {
            return Task.FromResult(PublishResult.Fail("GOG Galaxy integration not yet implemented"));
        }
    }
}
