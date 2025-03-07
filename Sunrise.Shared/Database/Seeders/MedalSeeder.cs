using Microsoft.EntityFrameworkCore;
using osu.Shared;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Objects.Serializable;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Seeders;

public class MedalConditionContext
{
    public UserStats user { get; set; }
    public Score score { get; set; }
    public Beatmap beatmap { get; set; }
}

public static class MedalSeeder
{
    public static async Task SeedMedals(DbContext context, CancellationToken ct = default)
    {

        List<List<Medal>> medalsGroups =
        [
            SkillMedals,
            ModsMedals,
            CustomMedals.Select(p => p.Key).ToList()
        ];

        var medals = medalsGroups.SelectMany(m => m).ToList();
        await AddOrUpdateMedals(context, medals, ct);

        await context.SaveChangesAsync(ct);
    }

    private static async Task AddOrUpdateMedals(DbContext context, List<Medal> medals, CancellationToken ct)
    {
        var existingMedals = await context.Set<Medal>().Include(m => m.File).ToListAsync(cancellationToken: ct);

        var medalsToAdd = new List<Medal>();

        foreach (var medal in medals)
        {
            var existingMedal = existingMedals.FirstOrDefault(m => m.Id == medal.Id);

            if (existingMedal != null)
            {
                if (existingMedal.File != null)
                {
                    var medalFilePath = CustomMedals.FirstOrDefault(p => p.Key.Id == existingMedal.Id).Value;
                    if (medalFilePath != null)
                        existingMedal.File.Path = medalFilePath;
                }

                medal.FileId = existingMedal.FileId;
                medal.File = existingMedal.File;

                context.Entry(existingMedal).CurrentValues.SetValues(medal);
            }
            else
            {
                var isMedalCustom = medal.FileUrl == null;

                if (isMedalCustom)
                {
                    var medalFilePath = CustomMedals.FirstOrDefault(p => p.Key.Id == medal.Id).Value;
                    if (medalFilePath == null)
                        throw new Exception($"Medal {medal.Id} doesn't include either FileUrl or FileId");

                    var medalImageEntry = await context.Set<MedalFile>().Where(mf => mf.Path == medalFilePath).FirstOrDefaultAsync(cancellationToken: ct);

                    if (medalImageEntry == null)
                    {
                        medalImageEntry = new MedalFile
                        {
                            Path = medalFilePath
                        };
                        await context.Set<MedalFile>().AddAsync(medalImageEntry, ct);
                        await context.SaveChangesAsync(ct);
                    }

                    medal.FileId = medalImageEntry.Id;
                    medal.File = medalImageEntry;
                }

                medalsToAdd.Add(medal);
            }
        }

        context.Set<Medal>().AddRange(medalsToAdd);
        await context.SaveChangesAsync(ct);
    }

    private static string FormatModIntroductionCondition(Mods mode)
    {
        return $"({nameof(MedalConditionContext.score)}.{nameof(Score.Mods)} & {(int)mode}) == {(int)mode}";
    }

    private static string FormatDifficultyRatingPassCondition(int difficultyRating)
    {
        return $"{nameof(MedalConditionContext.beatmap)}.{nameof(Beatmap.DifficultyRating)} >= {difficultyRating} && {nameof(MedalConditionContext.beatmap)}.{nameof(Beatmap.DifficultyRating)} < {difficultyRating + 1}";
    }

    private static string FormatDifficultyRatingFullComboCondition(int difficultyRating)
    {
        return $"{FormatDifficultyRatingPassCondition(difficultyRating)} && {nameof(MedalConditionContext.score)}.{nameof(Score.Perfect)}";
    }

    private static string FormatMaxComboCondition(int maxCombo)
    {
        return $"{nameof(MedalConditionContext.score)}.{nameof(Score.MaxCombo)} >= {maxCombo}";
    }

    private static string FormatTotalHitsCondition(int totalHits)
    {
        return $"{nameof(MedalConditionContext.user)}.{nameof(UserStats.TotalHits)} >= {totalHits}";
    }

    private static string FormatPlayCountCondition(int playCount)
    {
        return $"{nameof(MedalConditionContext.user)}.{nameof(UserStats.PlayCount)} >= {playCount}";
    }

    private static string FormatPlayBeatmapSetIdCondition(int beatmapSetId)
    {
        return $"{nameof(MedalConditionContext.beatmap)}.{nameof(Beatmap.BeatmapsetId)} == {beatmapSetId}";
    }

    // @formatter:off
    private static readonly List<Medal> SkillMedals =
    [
        new() { Id = 1, Name = "Rising Star", Description = "Can't go forward without the first steps.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-pass-1", Condition = FormatDifficultyRatingPassCondition(1) },
        new() { Id = 2, Name = "Constellation Prize", Description = "Definitely not a consolation prize. Now things start getting hard!", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-pass-2", Condition = FormatDifficultyRatingPassCondition(2) },
        new() { Id = 3, Name = "Building Confidence", Description = "Oh, you've SO got this.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-pass-3", Condition = FormatDifficultyRatingPassCondition(3) },
        new() { Id = 4, Name = "Insanity Approaches", Description = "You're not twitching, you're just ready.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-pass-4", Condition = FormatDifficultyRatingPassCondition(4) },
        new() { Id = 5, Name = "These Clarion Skies", Description = "Everything seems so clear now.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-pass-5", Condition = FormatDifficultyRatingPassCondition(5) },
        new() { Id = 6, Name = "Above and Beyond", Description = "A cut above the rest.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-pass-6", Condition = FormatDifficultyRatingPassCondition(6) },
        new() { Id = 7, Name = "Supremacy", Description = "All marvel before your prowess.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-pass-7", Condition = FormatDifficultyRatingPassCondition(7) },
        new() { Id = 8, Name = "Absolution", Description = "My god, you're full of stars!", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-pass-8", Condition = FormatDifficultyRatingPassCondition(8) },
        new() { Id = 9, Name = "Event Horizon", Description = "No force dares to pull you under.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-pass-9", Condition = FormatDifficultyRatingPassCondition(9) },
        new() { Id = 10, Name = "Phantasm", Description = "Fevered is your passion, extraordinary is your skill.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-pass-10", Condition = FormatDifficultyRatingPassCondition(10) },
        new() { Id = 11, Name = "Totality", Description = "All the notes. Every single one.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-fc-1", Condition = FormatDifficultyRatingFullComboCondition(1) },
        new() { Id = 12, Name = "Business As Usual", Description = "Two to go, please.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-fc-2", Condition = FormatDifficultyRatingFullComboCondition(2) },
        new() { Id = 13, Name = "Building Steam", Description = "Hey, this isn't so bad.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-fc-3", Condition = FormatDifficultyRatingFullComboCondition(3) },
        new() { Id = 14, Name = "Moving Forward", Description = "Bet you feel good about that.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-fc-4", Condition = FormatDifficultyRatingFullComboCondition(4) },
        new() { Id = 15, Name = "Paradigm Shift", Description = "Surprisingly difficult.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-fc-5", Condition = FormatDifficultyRatingFullComboCondition(5) },
        new() { Id = 16, Name = "Anguish Quelled", Description = "Don't choke.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-fc-6", Condition = FormatDifficultyRatingFullComboCondition(6) },
        new() { Id = 17, Name = "Never Give Up", Description = "Excellence is its own reward.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-fc-7", Condition = FormatDifficultyRatingFullComboCondition(7) },
        new() { Id = 18, Name = "Aberration", Description = "They said it couldn't be done. They were wrong.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-fc-8", Condition = FormatDifficultyRatingFullComboCondition(8) },
        new() { Id = 19, Name = "Chosen", Description = "Reign among the Prometheans, where you belong.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-fc-9", Condition = FormatDifficultyRatingFullComboCondition(9) },
        new() { Id = 20, Name = "Unfathomable", Description = "You have no equal.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-skill-fc-10", Condition = FormatDifficultyRatingFullComboCondition(10) },
        
        new() { Id = 21, Name = "500 Combo", Description = "500 big ones! You're moving up in the world!", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-combo-500", Condition = FormatMaxComboCondition(500) },
        new() { Id = 22, Name = "750 Combo", Description = "750 notes back to back? Woah.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-combo-750", Condition = FormatMaxComboCondition(750) },
        new() { Id = 23, Name = "1,000 Combo", Description = "A thousand reasons why you rock at this game.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-combo-1000", Condition = FormatMaxComboCondition(1000) },
        new() { Id = 24, Name = "2,000 Combo", Description = "Nothing can stop you now.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-combo-2000", Condition = FormatMaxComboCondition(2000) },

        new() { Id = 25, Name = "5,000 Plays", Description = "There's a lot more where that came from.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-plays-5000", Condition = FormatPlayCountCondition(5000) },
        new() { Id = 26, Name = "15,000 Plays", Description = "Must.. click.. circles..", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-plays-15000", Condition = FormatPlayCountCondition(15000) },
        new() { Id = 27, Name = "25,000 Plays", Description = "There's no going back.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-plays-25000", Condition = FormatPlayCountCondition(25000) },
        new() { Id = 28, Name = "50,000 Plays", Description = "You're here forever.", GameMode = GameMode.Standard, Category = MedalCategory.Skill, FileUrl = "osu-plays-50000", Condition = FormatPlayCountCondition(50000) },
        
        new() { Id = 29, Name = "My First Don", Description = "Marching to the beat of your own drum. Literally.", GameMode = GameMode.Taiko,  Category = MedalCategory.Skill, FileUrl = "taiko-skill-pass-1", Condition = FormatDifficultyRatingPassCondition(1) },
        new() { Id = 30, Name = "Katsu Katsu Katsu", Description = "Hora! Ikuzo!", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-skill-pass-2", Condition = FormatDifficultyRatingPassCondition(2) },
        new() { Id = 31, Name = "Not Even Trying", Description = "Muzukashii? Not even.", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-skill-pass-3", Condition = FormatDifficultyRatingPassCondition(3) },
        new() { Id = 32, Name = "Face Your Demons", Description = "The first trials are now behind you, but are you a match for the Oni?", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-skill-pass-4", Condition = FormatDifficultyRatingPassCondition(4) },
        new() { Id = 33, Name = "The Demon Within", Description = "No rest for the wicked.", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-skill-pass-5", Condition = FormatDifficultyRatingPassCondition(5) },
        new() { Id = 34, Name = "Drumbreaker", Description = "Too strong.", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-skill-pass-6", Condition = FormatDifficultyRatingPassCondition(6) },
        new() { Id = 35, Name = "The Godfather", Description = "You are the Don of Dons.", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-skill-pass-7", Condition = FormatDifficultyRatingPassCondition(7) },
        new() { Id = 36, Name = "Rhythm Incarnate", Description = "Feel the beat. Become the beat.", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-skill-pass-8", Condition = FormatDifficultyRatingPassCondition(8) },
        
        new() { Id = 37, Name = "Keeping Time", Description = "Don, then katsu. Don, then katsu..", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-skill-fc-1", Condition = FormatDifficultyRatingFullComboCondition(1) },
        new() { Id = 38, Name = "To Your Own Beat", Description = "Straight and steady.", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-skill-fc-2", Condition = FormatDifficultyRatingFullComboCondition(2) },
        new() { Id = 39, Name = "Big Drums", Description = "Bigger scores to match.", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-skill-fc-3", Condition = FormatDifficultyRatingFullComboCondition(3) },
        new() { Id = 40, Name = "Adversity Overcome", Description = "Difficult? Not for you.", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-skill-fc-4", Condition = FormatDifficultyRatingFullComboCondition(4) },
        new() { Id = 41, Name = "Demonslayer", Description = "An Oni felled forevermore.", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-skill-fc-5", Condition = FormatDifficultyRatingFullComboCondition(5) },
        new() { Id = 42, Name = "'Rhythm's Call", Description = "Heralding true skill.", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-skill-fc-6", Condition = FormatDifficultyRatingFullComboCondition(6) },
        new() { Id = 43, Name = "Time Everlasting", Description = "Not a single beat escapes you.", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-skill-fc-7", Condition = FormatDifficultyRatingFullComboCondition(7) },
        new() { Id = 44, Name = "The Drummer's Throne", Description = "Percussive brilliance befitting royalty alone.", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-skill-fc-8", Condition = FormatDifficultyRatingFullComboCondition(8) },
        
        new() { Id = 45, Name = "30,000 Drum Hits", Description = "Did that drum have a face?", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-hits-30000", Condition = FormatTotalHitsCondition(30000) },
        new() { Id = 46, Name = "300,000 Drum Hits", Description = "The rhythm never stops.", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-hits-300000", Condition = FormatTotalHitsCondition(300000) },
        new() { Id = 47, Name = "3,000,000 Drum Hits", Description = "Truly, the Don of dons.", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-hits-3000000", Condition = FormatTotalHitsCondition(3000000) },
        new() { Id = 48, Name = "30,000,000 Drum Hits", Description = "Your rhythm, eternal.", GameMode = GameMode.Taiko, Category = MedalCategory.Skill, FileUrl = "taiko-hits-30000000", Condition = FormatTotalHitsCondition(30000000) },
        
        new() { Id = 49, Name = "A Slice Of Life", Description = "Hey, this fruit catching business isn't bad.", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-skill-pass-1", Condition = FormatDifficultyRatingPassCondition(1) },
        new() { Id = 50, Name = "Dashing Ever Forward", Description = "Fast is how you do it.", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-skill-pass-2", Condition = FormatDifficultyRatingPassCondition(2) },
        new() { Id = 51, Name = "Zesty Disposition", Description = "No scurvy for you, not with that much fruit.", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-skill-pass-3", Condition = FormatDifficultyRatingPassCondition(3) },
        new() { Id = 52, Name = "Hyperdash ON!", Description = "Time and distance is no obstacle to you.", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-skill-pass-4", Condition = FormatDifficultyRatingPassCondition(4) },
        new() { Id = 53, Name = "It's Raining Fruit", Description = "And you can catch them all.", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-skill-pass-5", Condition = FormatDifficultyRatingPassCondition(5) },
        new() { Id = 54, Name = "Fruit Ninja", Description = "Legendary techniques.", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-skill-pass-6", Condition = FormatDifficultyRatingPassCondition(6) },
        new() { Id = 55, Name = "Dreamcatcher", Description = "No fruit, only dreams now.", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-skill-pass-7", Condition = FormatDifficultyRatingPassCondition(7) },
        new() { Id = 56, Name = "Lord of the Catch", Description = "Your kingdom kneels before you.", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-skill-pass-8", Condition = FormatDifficultyRatingPassCondition(8) },
        
        new() { Id = 57, Name = "Sweet And Sour", Description = "Apples and oranges, literally.", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-skill-fc-1", Condition = FormatDifficultyRatingFullComboCondition(1) },
        new() { Id = 58, Name = "Reaching The Core", Description = "The seeds of future success.", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-skill-fc-2", Condition = FormatDifficultyRatingFullComboCondition(2) },
        new() { Id = 59, Name = "Clean Platter", Description = "Clean only of failure. It is completely full, otherwise.", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-skill-fc-3", Condition = FormatDifficultyRatingFullComboCondition(3) },
        new() { Id = 60, Name = "Between The Rain", Description = "No umbrella needed.", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-skill-fc-4", Condition = FormatDifficultyRatingFullComboCondition(4) },
        new() { Id = 61, Name = "Addicted", Description = "That was an overdose?", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-skill-fc-5", Condition = FormatDifficultyRatingFullComboCondition(5) },
        new() { Id = 62, Name = "Quickening", Description = "A dash above normal limits.", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-skill-fc-6", Condition = FormatDifficultyRatingFullComboCondition(6) },
        new() { Id = 63, Name = "Supersonic", Description = "Faster than is reasonably necessary.", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-skill-fc-7", Condition = FormatDifficultyRatingFullComboCondition(7) },
        new() { Id = 64, Name = "Dashing Scarlet", Description = "Speed beyond mortal reckoning.", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-skill-fc-8", Condition = FormatDifficultyRatingFullComboCondition(8) },
        
        new() { Id = 65, Name = "Catch 20,000 fruits", Description = "That is a lot of dietary fiber.", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-hits-20000", Condition = FormatTotalHitsCondition(20000) },
        new() { Id = 66, Name = "Catch 200,000 fruits", Description = "So, I heard you like fruit...", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-hits-200000", Condition = FormatTotalHitsCondition(200000) },
        new() { Id = 67, Name = "Catch 2,000,000 fruits", Description = "Downright healthy.", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-hits-2000000", Condition = FormatTotalHitsCondition(2000000) },
        new() { Id = 68, Name = "Catch 20,000,000 fruits", Description = "Nothing left behind.", GameMode = GameMode.CatchTheBeat, Category = MedalCategory.Skill, FileUrl = "fruits-hits-20000000", Condition = FormatTotalHitsCondition(20000000) },
        
        new() { Id = 69, Name = "First Steps", Description = "It isn't 9-to-4, but 1-to-9. Keys, that is.", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-skill-pass-1", Condition = FormatDifficultyRatingPassCondition(1) },
        new() { Id = 70, Name = "No Normal Player", Description = "Not anymore, at least.", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-skill-pass-2", Condition = FormatDifficultyRatingPassCondition(2) },
        new() { Id = 71, Name = "Impulse Drive", Description = "Not quite hyperspeed, but getting close.", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-skill-pass-3", Condition = FormatDifficultyRatingPassCondition(3) },
        new() { Id = 72, Name = "Hyperspeed", Description = "Woah.", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-skill-pass-4", Condition = FormatDifficultyRatingPassCondition(4) },
        new() { Id = 73, Name = "Ever Onwards", Description = "Another challenge is just around the corner.", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-skill-pass-5", Condition = FormatDifficultyRatingPassCondition(5) },
        new() { Id = 74, Name = "Another Surpassed", Description = "Is there no limit to your skills?", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-skill-pass-6", Condition = FormatDifficultyRatingPassCondition(6) },
        new() { Id = 75, Name = "Extra Credit", Description = "See me after class.", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-skill-pass-7", Condition = FormatDifficultyRatingPassCondition(7) },
        new() { Id = 76, Name = "Maniac", Description = "There's just no stopping you.", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-skill-pass-8", Condition = FormatDifficultyRatingPassCondition(8) },
        
        new() { Id = 77, Name = "Keystruck", Description = "The beginning of a new story.", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-skill-fc-1", Condition = FormatDifficultyRatingFullComboCondition(1) },
        new() { Id = 78, Name = "Keying In", Description = "Finding your groove.", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-skill-fc-2", Condition = FormatDifficultyRatingFullComboCondition(2) },
        new() { Id = 79, Name = "Hyperflow", Description = "You can *feel* the rhythm.", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-skill-fc-3", Condition = FormatDifficultyRatingFullComboCondition(3) },
        new() { Id = 80, Name = "Breakthrough", Description = "Many skills mastered, rolled into one.", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-skill-fc-4", Condition = FormatDifficultyRatingFullComboCondition(4) },
        new() { Id = 81, Name = "Everything Extra", Description = "Giving your all is giving everything you have.", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-skill-fc-5", Condition = FormatDifficultyRatingFullComboCondition(5) },
        new() { Id = 82, Name = "Level Breaker", Description = "Finesse beyond reason.", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-skill-fc-6", Condition = FormatDifficultyRatingFullComboCondition(6) },
        new() { Id = 83, Name = "Step Up", Description = "A precipice rarely seen.", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-skill-fc-7", Condition = FormatDifficultyRatingFullComboCondition(7) },
        new() { Id = 84, Name = "Behind The Veil", Description = "Supernatural!", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-skill-fc-8", Condition = FormatDifficultyRatingFullComboCondition(8) },
        
        new() { Id = 85, Name = "40,000 Keys", Description = "Just the start of the rainbow.", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-hits-40000", Condition = FormatTotalHitsCondition(40000) },
        new() { Id = 86, Name = "400,000 Keys", Description = "Four hundred thousand and still not even close.", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-hits-400000", Condition = FormatTotalHitsCondition(400000) },
        new() { Id = 87, Name = "4,000,000 Keys", Description = "Is this the end of the rainbow?", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-hits-4000000", Condition = FormatTotalHitsCondition(4000000) },
        new() { Id = 88, Name = "40,000,000 Keys", Description = "The rainbow is eternal.", GameMode = GameMode.Mania, Category = MedalCategory.Skill, FileUrl = "mania-hits-40000000", Condition = FormatTotalHitsCondition(40000000) }
    ];

    private static readonly List<Medal> ModsMedals =
    [
         new() { Id = 89, Name = "Finality", Description = "High stakes, no regrets.", Category = MedalCategory.ModIntroduction, FileUrl = "all-intro-suddendeath", Condition = FormatModIntroductionCondition(Mods.SuddenDeath) },
         new() { Id = 90, Name = "Perfectionist", Description = "Accept nothing but the best.", Category = MedalCategory.ModIntroduction, FileUrl = "all-intro-perfect", Condition = FormatModIntroductionCondition(Mods.Perfect) },
         new() { Id = 91, Name = "Rock Around The Clock", Description = "You can't stop the rock.", Category = MedalCategory.ModIntroduction, FileUrl = "all-intro-hardrock", Condition = FormatModIntroductionCondition(Mods.HardRock) },
         new() { Id = 92, Name = "Time And A Half", Description = "Having a right ol' time. One and a half of them, almost.", Category = MedalCategory.ModIntroduction, FileUrl = "all-intro-doubletime", Condition = FormatModIntroductionCondition(Mods.DoubleTime) },
         new() { Id = 93, Name = "Sweet Rave Party", Description = "Founded in the fine tradition of changing things that were just fine as they were.", Category = MedalCategory.ModIntroduction, FileUrl = "all-intro-nightcore", Condition = FormatModIntroductionCondition(Mods.Nightcore) },
         new() { Id = 94, Name = "Blindsight", Description = "I can see just perfectly.", Category = MedalCategory.ModIntroduction, FileUrl = "all-intro-hidden", Condition = FormatModIntroductionCondition(Mods.Hidden) },
         new() { Id = 95, Name = "Are You Afraid Of The Dark?", Description = "Harder than it looks, probably because it's hard to look.",  Category = MedalCategory.ModIntroduction, FileUrl = "all-intro-flashlight", Condition = FormatModIntroductionCondition(Mods.Flashlight) },
         new() { Id = 96, Name = "Dial It Right Back", Description = "Sometimes you just want to take it easy.",  Category = MedalCategory.ModIntroduction, FileUrl = "all-intro-easy", Condition = FormatModIntroductionCondition(Mods.Easy) },
         new() { Id = 97, Name = "Risk Averse", Description = "Safety nets are fun!", Category = MedalCategory.ModIntroduction, FileUrl = "all-intro-nofail", Condition = FormatModIntroductionCondition(Mods.NoFail) },
         new() { Id = 98, Name = "Slowboat", Description = "You got there. Eventually.", Category = MedalCategory.ModIntroduction, FileUrl = "all-intro-halftime", Condition = FormatModIntroductionCondition(Mods.HalfTime) },
         new() { Id = 99, Name = "Burned Out", Description = "One cannot always spin to win.", Category = MedalCategory.ModIntroduction, FileUrl = "all-intro-spunout", Condition = FormatModIntroductionCondition(Mods.SpunOut) }
    ];
    
     private static readonly Dictionary<Medal,string?> CustomMedals = new()
     {
            {    
                new Medal { Id = 100, Name = "Man this DJ is fire", Description = "Just don't listen to the original. It's not as good.", Category = MedalCategory.BeatmapHunt, Condition = FormatPlayBeatmapSetIdCondition(1357624) },
                "Files/Medals/all-secret-thisdjisfire.png"
            },
             {    
                new Medal { Id = 101, Name = "Heat abnormal", Description = "Is it just me, or does my head get dizzy from all the heat?", Category = MedalCategory.BeatmapHunt, Condition = FormatPlayBeatmapSetIdCondition(2058976) },
                "Files/Medals/all-secret-heat-abnormal.png"
            }
     };
    // @formatter:on
}