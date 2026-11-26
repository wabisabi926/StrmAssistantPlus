using Emby.Naming.Common;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using StrmAssistant.Common;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StrmAssistant.Tests
{
    public class SubtitleApiContractTests
    {
        [Fact]
        public void FindExternalSubtitleStreamsMethod_MatchesExactSignature()
        {
            var method = SubtitleApi.FindExternalSubtitleStreamsMethod(typeof(FakeResolver));

            Assert.NotNull(method);
            Assert.Equal(nameof(FakeResolver.Resolve), method.Name);
        }

        [Fact]
        public void FindExternalSubtitleStreamsMethod_RejectsWrongReturnType()
        {
            var method = SubtitleApi.FindExternalSubtitleStreamsMethod(typeof(WrongResolver));

            Assert.Null(method);
        }

        [Fact]
        public void FindSubtitleProbeUpdaterType_FindsUniqueExactContract()
        {
            var type = SubtitleApi.FindSubtitleProbeUpdaterType(new[]
            {
                typeof(WrongReturnProbe), typeof(WrongParametersProbe), typeof(ExactProbe)
            });

            Assert.Equal(typeof(ExactProbe), type);
        }

        [Fact]
        public void FindSubtitleProbeUpdaterType_RejectsAmbiguousContracts()
        {
            var type = SubtitleApi.FindSubtitleProbeUpdaterType(new[] { typeof(ExactProbe), typeof(AnotherExactProbe) });

            Assert.Null(type);
        }

        private class FakeResolver
        {
            internal List<MediaStream> Resolve(BaseItem item, int startIndex, IDirectoryService directoryService,
                LibraryOptions libraryOptions, NamingOptions namingOptions, bool clearCache) => null;
        }

        private class WrongResolver
        {
            internal string Resolve(BaseItem item, int startIndex, IDirectoryService directoryService,
                LibraryOptions libraryOptions, NamingOptions namingOptions, bool clearCache) => null;
        }

        private class ExactProbe
        {
            public ExactProbe(IMediaProbeManager mediaProbeManager) { }

            public Task<bool> Probe(BaseItem item, MediaStream stream, MetadataRefreshOptions options,
                LibraryOptions libraryOptions, CancellationToken cancellationToken) => Task.FromResult(true);
        }

        private class AnotherExactProbe
        {
            public AnotherExactProbe(IMediaProbeManager mediaProbeManager) { }

            public Task<bool> Probe(BaseItem item, MediaStream stream, MetadataRefreshOptions options,
                LibraryOptions libraryOptions, CancellationToken cancellationToken) => Task.FromResult(true);
        }

        private class WrongReturnProbe
        {
            public WrongReturnProbe(IMediaProbeManager mediaProbeManager) { }

            public Task<string> Probe(BaseItem item, MediaStream stream, MetadataRefreshOptions options,
                LibraryOptions libraryOptions, CancellationToken cancellationToken) => Task.FromResult("wrong");
        }

        private class WrongParametersProbe
        {
            public WrongParametersProbe(IMediaProbeManager mediaProbeManager) { }

            public Task<bool> Probe(BaseItem item, MediaStream stream, MetadataRefreshOptions options,
                LibraryOptions libraryOptions) => Task.FromResult(true);
        }
    }
}
