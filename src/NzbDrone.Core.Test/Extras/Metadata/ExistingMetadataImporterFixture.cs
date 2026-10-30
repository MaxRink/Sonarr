using System.Collections.Generic;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Extras.Files;
using NzbDrone.Core.Extras.Metadata;
using NzbDrone.Core.Extras.Metadata.Files;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Extras.Metadata
{
    [TestFixture]
    public class ExistingMetadataImporterFixture : CoreTest<ExistingMetadataImporter>
    {
        private Series _series;
        private Mock<IMetadata> _consumer;
        private List<string> _filesOnDisk;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                                     .With(s => s.Path = @"C:\Test\TV\Series Title".AsOsAgnostic())
                                     .Build();

            _filesOnDisk = new List<string> { Path.Combine(_series.Path, "folder.jpg") };

            _consumer = new Mock<IMetadata>();
            _consumer.Setup(c => c.FindMetadataFile(_series, It.IsAny<string>()))
                     .Returns(new MetadataFile
                     {
                         SeriesId = _series.Id,
                         Consumer = "TestMetadata",
                         Type = MetadataType.SeriesImage,
                         RelativePath = "folder.jpg"
                     });

            // Every consumer is registered, regardless of whether it is enabled
            Mocker.SetConstant<IEnumerable<IMetadata>>(new[] { _consumer.Object });

            Mocker.GetMock<IExtraFileService<MetadataFile>>()
                  .Setup(s => s.GetFilesBySeries(_series.Id))
                  .Returns(new List<MetadataFile>());
        }

        private void GivenEnabledConsumers(params IMetadata[] consumers)
        {
            Mocker.GetMock<IMetadataFactory>()
                  .Setup(s => s.Enabled())
                  .Returns(consumers.ToList());
        }

        [Test]
        public void should_not_import_existing_metadata_for_disabled_consumers()
        {
            GivenEnabledConsumers();

            var results = Subject.ProcessFiles(_series, _filesOnDisk, new List<string>(), null).ToList();

            results.Should().BeEmpty();

            _consumer.Verify(c => c.FindMetadataFile(It.IsAny<Series>(), It.IsAny<string>()), Times.Never());

            Mocker.GetMock<IExtraFileService<MetadataFile>>()
                  .Verify(s => s.Upsert(It.Is<List<MetadataFile>>(l => l.Count == 0)), Times.Once());
        }

        [Test]
        public void should_import_existing_metadata_for_enabled_consumers()
        {
            GivenEnabledConsumers(_consumer.Object);

            var results = Subject.ProcessFiles(_series, _filesOnDisk, new List<string>(), null).ToList();

            results.Should().HaveCount(1);
            results.First().As<MetadataFile>().Extension.Should().Be(".jpg");

            Mocker.GetMock<IExtraFileService<MetadataFile>>()
                  .Verify(s => s.Upsert(It.Is<List<MetadataFile>>(l => l.Count == 1)), Times.Once());
        }
    }
}
