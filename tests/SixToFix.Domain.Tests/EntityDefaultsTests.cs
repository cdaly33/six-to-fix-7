using SixToFix.Domain.Entities;
using SixToFix.Domain.Enums;
using Xunit;
namespace SixToFix.Domain.Tests;
public sealed class EntityDefaultsTests { [Fact] public void Tenant_DefaultValues_AreCorrect(){var t=new Tenant();Assert.Equal(string.Empty,t.Name);Assert.Equal(string.Empty,t.Slug);Assert.True(t.IsActive);} [Fact] public void PillarContent_DefaultValues_AreCorrect(){var e=new PillarContent();Assert.Equal("{}",e.BodyJson);} [Fact] public void UserPillarProgress_DefaultValues_AreCorrect(){var e=new UserPillarProgress();Assert.Equal(0,e.PercentComplete);} [Fact] public void PlaybookTemplate_DefaultValues_AreCorrect(){var e=new PlaybookTemplate();Assert.Equal(PlaybookTemplateStatus.Draft,e.Status);} }
