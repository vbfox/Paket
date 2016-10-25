﻿using System;
using System.Net;
using Moq;
using NUnit.Framework;
using Paket.Bootstrapper.DownloadStrategies;
using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper.Tests.DownloadStrategies
{
    [TestFixture]
    public class CacheDownloadStrategyTest
    {
        private CacheDownloadStrategy sut;
        private Mock<IDownloadStrategy> mockEffectiveStrategy;
        private Mock<IFileSystemProxy> mockFileProxy;

        [SetUp]
        public void Setup()
        {
            mockEffectiveStrategy = new Mock<IDownloadStrategy>();
            mockFileProxy = new Mock<IFileSystemProxy>();
            sut = new CacheDownloadStrategy(mockEffectiveStrategy.Object, mockFileProxy.Object);
        }

        [Test]
        public void CreateStrategy_With_NoEffective()
        {
            //arrange
            //act
            //assert
            Assert.Throws<ArgumentException>(() => new CacheDownloadStrategy(null, mockFileProxy.Object));
        }

        [Test]
        public void CreateStrategy_EffectiveStrategyHasFallback()
        {
            //arrange
            mockEffectiveStrategy.SetupGet(x => x.FallbackStrategy).Returns(new Mock<IDownloadStrategy>().Object);

            //act
            //assert
            Assert.Throws<ArgumentException>(() => new CacheDownloadStrategy(mockEffectiveStrategy.Object, mockFileProxy.Object));
        }

        [Test]
        public void Name()
        {
            //arrange
            mockEffectiveStrategy.SetupGet(x => x.Name).Returns("any");
            //act
            //assert
            Assert.That(sut.Name, Is.EqualTo("any - cached"));
        }

        [Test]
        public void GetLatestVersion_NonPrerelease()
        {
            //arrange
            mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Returns("any");

            //act
            var result = sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo("any"));
            mockEffectiveStrategy.Verify();
        }
        
        [Test]
        public void GetLatestVersion_EffectiveStrategyFails_UseFallback()
        {
            //arrange
            mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Throws<WebException>().Verifiable();
            var mockFallback = new Mock<IDownloadStrategy>();
            mockEffectiveStrategy.SetupGet(x => x.FallbackStrategy).Returns(mockFallback.Object);
            mockFileProxy.Setup(x => x.GetDirectories(It.IsAny<string>())).Returns(new[] { "1.0" });

            //act
            var result = sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo("1.0"));
            mockFallback.Verify();
        }

        [Test]
        public void GetLatestVersion_NoFallBackStrategy_UseBestCachedVersion()
        {
            //arrange
            mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Throws<WebException>().Verifiable();
            mockFileProxy.Setup(x => x.GetDirectories(It.IsAny<string>())).Returns(new[] {"2.1", "2.2"});

            //act
            var result = sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo("2.2"));
            mockEffectiveStrategy.Verify();
        }

        [Test]
        public void GetLatestVersion_NoFallBackStrategy_UseBestCachedVersion_CanHandleWrongSubdirectories()
        {
            //arrange
            mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Throws<WebException>().Verifiable();
            mockFileProxy.Setup(x => x.GetDirectories(It.IsAny<string>())).Returns(new[] { "2.1", "2.2", "wrongVersion" });

            //act
            var result = sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo("2.2"));
        }

        [Test]
        public void GetLatestVersion_NoFallBackStrategy_UseBestCachedVersion_IgnorePrereleaseSubFolder()
        {
            //arrange
            mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Throws<WebException>().Verifiable();
            mockFileProxy.Setup(x => x.GetDirectories(It.IsAny<string>())).Returns(new[] { "2.1", "2.2", "2.3-alpha" });

            //act
            var result = sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo("2.2"));
        }

        [Test]
        public void DownloadVersion_UseCachedVersion()
        {
            //arrange
            mockFileProxy.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);

            //act
            sut.DownloadVersion("any", "any");

            //assert
            mockEffectiveStrategy.Verify(x => x.DownloadVersion(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            mockFileProxy.Verify(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        }

        [Test]
        public void DownloadVersion_DownloadFromFallback()
        {
            //arrange
            mockFileProxy.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);

            //act
            sut.DownloadVersion("any", "any");

            //assert
            mockEffectiveStrategy.Verify(x => x.DownloadVersion("any", "any"));
            mockFileProxy.Verify(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        }

        [Test]
        public void SelfUpdate()
        {
            //arrange
            //act
            sut.SelfUpdate("any");

            //assert
            mockEffectiveStrategy.Verify(x => x.SelfUpdate("any"));
        }
        
    }
}
