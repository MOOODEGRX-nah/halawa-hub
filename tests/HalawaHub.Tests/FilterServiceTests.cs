using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using HalawaHub.Core.Models;
using Moq;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>
/// اختبارات FilterService — منطق نقي بدون UI state.
/// </summary>
public class FilterServiceTests
{
    private readonly FilterService _service = new();
    private readonly List<GameCardViewModel> _games;

    public FilterServiceTests()
    {
        _games = new List<GameCardViewModel>
        {
            CreateGame("Steam", "Counter-Strike 2", isFavorite: true, isInstalled: true),
            CreateGame("Steam", "Dota 2", isFavorite: false, isInstalled: true),
            CreateGame("Epic Games", "Fortnite", isFavorite: true, isInstalled: false),
            CreateGame("GOG", "The Witcher 3", isFavorite: false, isInstalled: true),
        };
    }

    [Fact]
    public void ApplyFilter_NavAll_ReturnsAllGames()
    {
        // Act
        var result = _service.ApplyFilter(_games, "", "الكل", "name", "الكل", "المفضلة", "المثبتة", "المضافة حديثًا");

        // Assert
        result.Should().HaveCount(4);
    }

    [Fact]
    public void ApplyFilter_NavFavorite_ReturnsOnlyFavorites()
    {
        // Act
        var result = _service.ApplyFilter(_games, "", "المفضلة", "name", "الكل", "المفضلة", "المثبتة", "المضافة حديثًا");

        // Assert
        result.Should().HaveCount(2);
        result.All(g => g.IsFavorite).Should().BeTrue();
    }

    [Fact]
    public void ApplyFilter_SearchQuery_CaseInsensitive()
    {
        // Act
        var result = _service.ApplyFilter(_games, "COUNTER", "", "name", "الكل", "المفضلة", "المثبتة", "المضافة حديثًا");

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Counter-Strike 2");
    }

    [Fact]
    public void ApplyFilter_PlatformLabel_ExtractsCorrectName()
    {
        // Act
        var result = _service.ApplyFilter(_games, "", "Steam (2)", "name", "الكل", "المفضلة", "المثبتة", "المضافة حديثًا");

        // Assert
        result.Should().HaveCount(2);
        result.All(g => g.Platform == "Steam").Should().BeTrue();
    }

    [Fact]
    public void ApplyFilter_SortByName_CorrectOrder()
    {
        // Act
        var result = _service.ApplyFilter(_games, "", "الكل", "name", "الكل", "المفضلة", "المثبتة", "المضافة حديثًا").ToList();

        // Assert
        result[0].Name.Should().Be("Counter-Strike 2");
        result[1].Name.Should().Be("Dota 2");
        result[2].Name.Should().Be("Fortnite");
        result[3].Name.Should().Be("The Witcher 3");
    }

    private GameCardViewModel CreateGame(string platform, string name, bool isFavorite, bool isInstalled)
    {
        var game = new GameInfo
        {
            Id = name.ToLower().Replace(" ", "-"),
            Name = name,
            Platform = platform,
            IsInstalled = isInstalled
        };
        var card = new GameCardViewModel(game, new List<IGameTool>());
        if (isFavorite) card.IsFavorite = true;
        return card;
    }
}
