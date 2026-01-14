// Packages/com.protosystem.core/Editor/Publishing/Platforms/Itch/ItchPublisher.cs
using System;
using System.Threading.Tasks;

namespace ProtoSystem.Publishing.Editor
{
    /// <summary>
    /// Издатель для itch.io (заглушка)
    /// </summary>
    public class ItchPublisher : IPlatformPublisher
    {
        private readonly ItchConfig _config;

        public string PlatformId => "itch";
        public string DisplayName => "itch.io";
        public bool IsSupported => false; // TODO: Реализовать

        public ItchPublisher(ItchConfig config)
        {
            _config = config;
        }

        public bool ValidateConfig(out string error)
        {
            error = "itch.io integration not yet implemented";
            return false;
        }

        public Task<PublishResult> UploadAsync(string buildPath, string branch, string description,
            IProgress<PublishProgress> progress = null)
        {
            return Task.FromResult(PublishResult.Fail("itch.io integration not yet implemented"));
        }

        public Task<PublishResult> PublishNewsAsync(PatchNotesEntry entry,
            IProgress<PublishProgress> progress = null)
        {
            return Task.FromResult(PublishResult.Fail("itch.io integration not yet implemented"));
        }

        public Task<PublishResult> SetLiveAsync(string buildId, string branch,
            IProgress<PublishProgress> progress = null)
        {
            return Task.FromResult(PublishResult.Fail("itch.io integration not yet implemented"));
        }
    }
}
