﻿using Avangardum.LifeArena.Server.Controllers;
using Avangardum.LifeArena.Server.Interfaces;
using Avangardum.LifeArena.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Avangardum.LifeArena.Server.UnitTests;

[TestFixture]
public class GameControllerTests
{
    private class MockGameService : IGameService
    {
        public MockGameService()
        {
            LivingCells = new bool[2, 2];
            LivingCells[0, 0] = true;
        }
        
        public event EventHandler? GenerationChanged;
        public bool[,] LivingCells { get; }
        public int Generation => 42;
        public int MaxCellsPerPlayerPerGeneration => 10;
        public TimeSpan TimeUntilNextGeneration => TimeSpan.FromSeconds(5);

        public TimeSpan NextGenerationInterval => TimeSpan.FromSeconds(5);

        public Exception? AddCellException { get; set; }
        
        public void AddCell(int x, int y, string playerId)
        {
            if (AddCellException != null) throw AddCellException;
            LivingCells[x, y] = true;
        }

        public int GetCellsLeftForPlayer(string playerId)
        {
            return playerId == Player1Id ? 5 : 10;
        }
    }
    
    private class MockUserIdProvider : IUserIdProvider
    {
        public string UserId { get; set; } = "Anonymous";
    }
    
    private class MockUserActivityManager : IUserActivityManager
    {
        public int ReportUserActivityCallCount { get; private set; }
        
        public void ReportUserActivity(string userId, DateOnly date)
        {
            ReportUserActivityCallCount++;
        }

        public int GetDailyActiveUsersCount(DateOnly date)
        {
            throw new NotImplementedException();
        }
    }
    
    private const string Player1Id = "John Doe";
    private const string Player2Id = "Joe Mama";
    
    #pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
    private MockGameService _gameService;
    private MockUserIdProvider _userIdProvider;
    private MockUserActivityManager _userActivityManager;
    private GameController _gameController;
    private ILivingCellsArrayPreserializer _livingCellsArrayPreserializer;
    #pragma warning restore CS8618
    
    [SetUp]
    public void Setup()
    {
        _gameService = new MockGameService();
        _userIdProvider = new MockUserIdProvider();
        _livingCellsArrayPreserializer = new LivingCellsArrayPreserializer();
        _userActivityManager = new MockUserActivityManager();
        _gameController = new GameController(_gameService, _userIdProvider, _livingCellsArrayPreserializer, _userActivityManager);
    }

    [Test]
    public void GetGameStateReturnsGameStateResponseWithDataFromGameService([Values(Player1Id, Player2Id)] string playerId)
    {
        var preserializedLivingCells = _livingCellsArrayPreserializer.Preserialize(_gameService.LivingCells);
        // var expectedResponse = new GameStateResponse(preserializedLivingCells, _gameService.Generation, 
        //     _gameService.TimeUntilNextGeneration, _gameService.GetCellsLeftForPlayer(playerId), _gameService.MaxCellsPerPlayerPerTurn);
        var expectedResponse = new GameStateResponse
        (
            LivingCells: preserializedLivingCells,
            Generation: _gameService.Generation,
            TimeUntilNextGeneration: _gameService.TimeUntilNextGeneration,
            NextGenerationInterval: _gameService.NextGenerationInterval,
            CellsLeft: _gameService.GetCellsLeftForPlayer(playerId),
            MaxCellsPerPlayerPerGeneration: _gameService.MaxCellsPerPlayerPerGeneration
        );
        _userIdProvider.UserId = playerId;
        var actualResponse = (_gameController.GetState() as OkObjectResult)?.Value as GameStateResponse;
        Assert.That(actualResponse, Is.Not.Null);
        Assert.That(actualResponse!.LivingCells, Is.EqualTo(expectedResponse.LivingCells));
        Assert.That(actualResponse.Generation, Is.EqualTo(expectedResponse.Generation));
        Assert.That(actualResponse.TimeUntilNextGeneration, Is.EqualTo(expectedResponse.TimeUntilNextGeneration));
        Assert.That(actualResponse.CellsLeft, Is.EqualTo(expectedResponse.CellsLeft));
        Assert.That(actualResponse.MaxCellsPerPlayerPerGeneration, Is.EqualTo(expectedResponse.MaxCellsPerPlayerPerGeneration));
    }

    [Test]
    public void AddCellAddsCellToGameServiceAndReturnsUpdatedState()
    {
        _userIdProvider.UserId = Player1Id;
        var response = (_gameController.AddCell(0, 1) as OkObjectResult)?.Value as GameStateResponse;
        Assert.That(response, Is.Not.Null);
        Assert.That(_gameService.LivingCells[0, 0], Is.True);
        Assert.That(_gameService.LivingCells[0, 1], Is.True);
        Assert.That(_gameService.LivingCells[1, 0], Is.False);
        Assert.That(_gameService.LivingCells[1, 1], Is.False);
        var expectedResponsePreserializedLivingCells = _livingCellsArrayPreserializer.Preserialize(_gameService.LivingCells);
        Assert.That(response!.LivingCells, Is.EqualTo(expectedResponsePreserializedLivingCells));
    }
    
    [Test]
    public void AddCellReturnsBadRequestWhenGameServiceAddCellThrowsArgumentException()
    {
        _userIdProvider.UserId = Player1Id;
        _gameService.AddCellException = new ArgumentException();
        var response = _gameController.AddCell(0, 0);
        Assert.That(response, Is.TypeOf<BadRequestResult>());
    }
    
    [Test]
    public void GetStateReportsUserActivity()
    {
        _gameController.GetState();
        _gameController.GetState();
        Assert.That(_userActivityManager.ReportUserActivityCallCount, Is.EqualTo(2));
    }
}