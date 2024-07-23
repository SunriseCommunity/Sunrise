namespace Sunrise.Services;

public class ScoreService
{
    private readonly PlayerRepository _playerRepository;
    private readonly BanchoService _banchoService;

    public ScoreService(PlayerRepository playerRepository, BanchoService banchoService)
    {
        _playerRepository = playerRepository;
        _banchoService = banchoService;
    }

    public void SubmitScore(Score score)
    {
        var player = _playerRepository.GetPlayerByUsername(score.Username);
        player.RankedScore += score.TotalScore * 2;
        player.TotalScore += score.TotalScore;
        player.PlayCount++;
        player.PerformancePoints += 1;

        _banchoService.SendUserData(player);
    }

    public void GetScores()
    {

    }

}