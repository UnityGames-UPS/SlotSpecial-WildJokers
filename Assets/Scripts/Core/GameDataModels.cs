using System;
using System.Collections.Generic;

#region Server Communication Models — Init

[Serializable]
public class InitData
{
    public string id = "initData";
    public ServerGameData gameData;
    public ServerFeatures features;
    public ServerUIData uiData;
    public ServerPlayer player;
    // Wild Jokers sends no jackpot block. Kept because the platform's separate "jackpot:sync"
    // event reuses these types, and the init-time read is already null-guarded.
    public JackpotData jackpotData;
}

[Serializable]
public class JackpotData
{
    public JackpotValues values;
}

[Serializable]
public class JackpotValues
{
    public string miniJackpot;
    public string minorJackpot;
    public string majorJackpot;
    public string grandJackpot;
}

[Serializable]
public class JackpotSyncData
{
    public string gameId;
    public JackpotValues values;
}

// The four platform jackpot tiers, in the order the platform ranks them.
public enum JackpotTier
{
    Grand,
    Major,
    Minor,
    Mini
}

// Clicking a jackpot tier asks the platform to open its own jackpot overlay. Fire-and-forget —
// the server sends no direct reply; any resulting value change arrives on the usual jackpot:sync.
[Serializable]
public class JackpotOpenRequest
{
    public string type = "JACKPOT_OPEN";
    public JackpotOpenPayload payload = new JackpotOpenPayload();
}

[Serializable]
public class JackpotOpenPayload
{
    public string tier;
}

[Serializable]
public class ServerGameData
{
    // The 20 paylines, each one row index per reel, left to right, row 0 at the top. Confirmed
    // against live wins: a win's matchedPositions trace lines[lineIndex] cell for cell. Also read
    // for the reel count, which falls back to 5.
    public List<List<int>> lines;
    public List<double> bets;
    // The number of paylines (20), and the bet multiplier: every line is always played, so total bet
    // = the selected bet x this. Confirmed on every captured spin: at bet 0.01 the result reports a
    // totalBetAmount of 0.2 and the balance drops by exactly that. There is no creditDivisor or
    // activeLine in this init.
    public int totalLines;
}

[Serializable]
public class ServerFeatures
{
    public ServerWildFeature wild;
    public ServerFreeSpinsFeature freeSpins;
    public ServerPickBonusFeature ballPickemBonus;
    public ServerPickBonusFeature sandbellPickBonus;
}

// Every feature names its symbol. The ids are nullable so a missing one reads as "not sent" rather
// than 0, which is a real symbol (the Wild).
[Serializable]
public class ServerWildFeature
{
    public int? symbolId;
    // "enabled" is sent and deliberately unbound: the Wild has no round to switch off.
}

[Serializable]
public class ServerFreeSpinsFeature
{
    public bool enabled;
    public int? symbolId;          // the Drum
    public int spinsCount;         // spins awarded per trigger: 10
    // Zero-based: one Drum on each of these reels triggers. [1,2,3] = reels 2, 3 and 4.
    public List<int> triggerReels;
    // Zero-based. Drums on these reels earn a near-miss respin of respinReel. The backend currently
    // respins with both Drums on reel 1 and none on reel 2, which cannot lead to a trigger — reported.
    public List<int> nearMissReels;
    public int respinReel;
}

// Ball Pick'em and Sandbell Pick send the same shape. How a round plays is still unconfirmed.
[Serializable]
public class ServerPickBonusFeature
{
    public bool enabled;
    public int? symbolId;          // the Ball or the Sandbell
    // 3. Whether that counts cells or reels is unconfirmed — two scatters can land on one reel.
    public int triggerCount;
    public int respinReel;
    // Whether these are multiples of the total bet or the line bet is unconfirmed.
    public List<double> availablePrizes;
    public double maxWinMultiplier;

    // Sent and deliberately unbound: "nearMissReels", "nearMissCountRange" and "triggerProbability".
    // Server-side maths the client has no use for, and the near-miss range contradicts triggerCount
    // (2 to 4 when 3 already triggers) — raised with the backend.
}

[Serializable]
public class ServerUIData
{
    // The name is inherited from the payline backends; it holds the symbol table.
    public PaylineData paylines;
}

[Serializable]
public class PaylineData
{
    public List<ServerSymbolInfo> symbols;
}

[Serializable]
public class ServerSymbolInfo
{
    public int id;
    public string name;         // "Wild", "Jack-in-the-box", "Drum", ... — display only, never used to find a role
    // "high", "low", "wild" or "scatter". This is what tells the special symbols apart; the init
    // sends no isSpecialSymbol flag and no displayName. Three symbols are scatters, so which feature
    // each one belongs to comes from the features' symbolId. "description" only repeats the group.
    public string group;
    // The paytable, ASCENDING and ENDING at a full-reel match: with 5 reels, index 0 = 2-of-a-kind,
    // 1 = 3, 2 = 4, 3 = 5. A 0 means that count doesn't pay — only Jack-in-the-box pays on 2. Credits,
    // not cash: one credit is the selected (per-line) bet, so J's 5-of-a-kind (100) pays 1.00 at bet
    // 0.01, confirmed. All zeros on the Wild and the three scatters. The converter reverses it,
    // because the client works descending from a full-reel match.
    public List<double> multiplier;
}

[Serializable]
public class ServerPlayer
{
    public double balance;
}

#endregion

#region Server Communication Models — Spin Result

[Serializable]
public class ServerSpinResponse
{
    public string id = "ResultData";
    public bool success;
    // Row-major and top-level, NOT nested under payload: 3 rows x 5 columns, values as strings.
    public List<List<string>> matrix;
    public ServerPayload payload;
    public ServerPlayerBalance player;
}

[Serializable]
public class ServerPlayerBalance
{
    // Nullable in case the server ever omits it; the converter falls back to a computed balance.
    public double? balance;
}

[Serializable]
public class ServerPayload
{
    // The line wins' total. Equal to grandTotalWin on every captured spin, but grandTotalWin is the
    // figure to display — a feature's prize may land in the gap.
    public double winAmount;
    // One entry per winning PAYLINE. Lines can share cells: three Jack-in-the-box 2-of-a-kinds on
    // lines 3, 9 and 13 all used the same two cells.
    public List<ServerLineWin> lineWins;

    // The spin's total, and the only figure to display. The balance moves by exactly this minus the
    // total bet (6066.65 -> 6066.50 on a 0.05 win at a 0.20 bet).
    public double grandTotalWin;

    // Sent and deliberately unbound:
    //  - "betAmountPerLine" and "totalBetAmount" (0.01 and 0.2 at the lowest bet): the client
    //    already knows its own bet.
    //  - "nearMissRespin", "isRespin" and "respinCount": the near-miss respin, bound when that
    //    feature is built. "matrix" is already the board after any respin.
    //  - "netReturnRatio" (grandTotalWin over total bet): the controller derives the same ratio.

    // Gold of Luck's free-games block. Wild Jokers never sends it, so it arrives null and reads as
    // "not in a round"; kept as the model for the free-spins state until Wild Jokers' own result is
    // captured (BonusFreeSpins.md §9.4).
    public ServerFreeGamesResult freeGames;
}

[Serializable]
public class ServerLineWin
{
    // Zero-based index into the init's lines. Confirmed: lineIndex 18's matchedPositions are
    // lines[18] = [2,0,0,0,2], cell for cell.
    public int lineIndex;
    // The paying cells, left to right, one per matched reel.
    public List<ServerCell> matchedPositions;
    // The symbol in each matched cell, Wilds included: [0,6,6] pays as three 10s.
    public List<int> symbolIds;
    public int matchLength;        // reels matched from the left: 2 to 5
    // Already inside winInCash; 1 on every captured win.
    public double lineMultiplier;
    // The line's win in cash: basePayout x lineMultiplier x the per-line bet.
    public double winInCash;

    // "basePayout" and "winInCredits" are sent and deliberately unbound: winInCash already carries
    // them, converted.
}

[Serializable]
public class ServerCell
{
    public int row;
    public int col;
}

// Gold of Luck — absent from Wild Jokers. Kept until Wild Jokers' free-spins result is captured.
[Serializable]
public class ServerFreeGamesResult
{
    // Spins played so far INCLUDING this one: 0 on the spin that triggers a round, 3 on the last of
    // three. After the spin, like remaining.
    public int played;
    // Spins left after this one. On the trigger spin itself, the whole award.
    public int remaining;
    // The round's running total: the sum of the FREE spins' wins. The trigger spin's own win is
    // not in it — it reads 0 there.
    public double totalFreeGamesWin;

    // Sent and deliberately unbound, because both read differently from their old namesakes:
    //  - "inFreeGames" is the state AFTER the spin. It is true on the trigger spin and false on the
    //    last free spin, itself a free game. It is not "this spin was free".
    //  - "triggered" stays true for every spin of a round, the last included, and is not a
    //    per-spin trigger flag.
    // "totalAwarded" is sent as well; the controller derives the same figure from played + remaining.
}

#endregion

#region Client-Side Spin Request

[Serializable]
public class SpinRequest
{
    public string type = "SPIN";
    public SpinPayload payload;
}

[Serializable]
public class SpinPayload
{
    // betIndex is the only field with a confirmed effect. The server owns free-spin state, so
    // there is no isFreeSpin flag to send. The mock client also sends "spins": 100; nobody knows
    // what it does, and this client deliberately does not send it.
    public int betIndex;
}

#endregion

#region Game Configuration (Client Side Converted)

[Serializable]
public class GameConfig
{
    public int reelCount = 5;
    // Every row the server sends is live and pays.
    public int rowCount = 3;

    // Total bet = selected bet x activeLine: the init's totalLines (20), since every payline is always
    // played. Also what the line-count label shows.
    public int activeLine = 20;

    // The paylines, one row index per reel, row 0 at the top. A win's WinLine.lineId indexes this.
    public List<List<int>> paylines;

    public List<double> availableBets;
    public List<SymbolInfo> symbols;

    // Resolved from the init symbol table. -1 means "not present", so an unresolved role can never
    // collide with a real symbol id the way a 0 default would — and -1 stays meaningful downstream:
    // SocketIOManager keeps both off the pre-spin placeholder board, and reads -1 as "no such
    // symbol, leave nothing out".
    public int wildSymbolId = -1;
    // A Gold of Luck leftover: that game had one scatter, Wild Jokers has three, so this resolves to
    // whichever comes last in the symbol table (the Sandbell). Still read by the scatter anticipation
    // and trigger code, which is reworked with the features — new code uses the per-feature ids below.
    public int scatterSymbolId = -1;

    // Each feature's symbol, from features.<feature>.symbolId. -1 when the init doesn't name one.
    public int freeSpinsSymbolId = -1;      // the Drum
    public int ballBonusSymbolId = -1;      // the Ball
    public int sandbellBonusSymbolId = -1;  // the Sandbell
}

[Serializable]
public class SymbolInfo
{
    public int id;
    public string name;
    // Descending from a full-reel match: index 0 = reelCount-of-a-kind, 1 = one fewer, and so on,
    // ending at minMatch — counts that don't pay are left off. In credits — multiply by the selected
    // (per-line) bet for cash.
    public List<double> multipliers;

    public bool isWild;
    public bool isScatter;
    // True for the Wild and the three scatters.
    public bool isSpecial;

    // Fewest matching symbols that pay: 2 for Jack-in-the-box, 3 for the rest. 0 when the symbol has
    // no paytable (the Wild and the scatters).
    public int minMatch;
}

#endregion

#region Player & Game State (Client Side)

[Serializable]
public class PlayerData
{
    public double balance;
    public int currentBetIndex;
}

[Serializable]
public class SpinResult
{
    // Column-major: [reel][row], the transpose of the server's row-major matrix.
    public List<List<int>> resultMatrix;

    // Everything the spin paid, from grandTotalWin. This is the figure to show and the one the
    // balance moved by.
    public double winAmount;
    // The line wins alone. Equal to winAmount on every captured spin; a feature's prize would land in
    // the gap.
    public double lineWinAmount;

    // One entry per winning payline.
    public List<WinLine> winLines;
    public PlayerData playerData;

    // The free-spins state, from Gold of Luck's freeGames block. Wild Jokers never sends that block,
    // so this always reads "not in a round" until its own result shape is bound.
    public FreeGameData freeGame;
}

/// <summary>
/// One winning payline.
/// </summary>
[Serializable]
public class WinLine
{
    // The payline that won: an index into GameConfig.paylines.
    public int lineId;
    // The symbol the line pays for. A Wild standing in never counts here, so a line of Wild, 10, 10
    // reads as the 10.
    public int symbolId;
    // Flat indices into the active grid: row * reelCount + col. The paying cells, left to right.
    public List<int> positions;
    public double winAmount;

    // Reels matched from the left: 2 to 5.
    public int matchCount;
    // Already inside winAmount. 1 when there is none.
    public double multiplier;
}

/// <summary>
/// The free-games facts for one spin, mapped from payload.freeGames onto the meanings the
/// controller was built around. The mapping lives in the converter so the controller never sees
/// the wire's post-spin flags — see ServerFreeGamesResult.
/// </summary>
[Serializable]
public class FreeGameData
{
    // Spins left AFTER this one. Retriggers are folded in, so it can go up as well as down.
    public int spinsRemaining;

    // Spins were awarded on this spin. The controller only reads it inside a round, where it marks a
    // retrigger. Gold of Luck set it from its wheel, which is gone, so nothing sets it until Wild
    // Jokers' trigger flag is bound (BonusFreeSpins.md §9.4).
    public bool spinsAwarded;

    // The round's running total, server-authoritative. Excludes the trigger spin's own win.
    public double roundWin;
}

#endregion

#region Enums

public enum GameState
{
    Initializing,
    Idle,
    Spinning,
    Stopping,
    ShowingWin
}

public enum SpinSpeed
{
    Normal,
    Turbo,
    QuickSpin
}

public enum WinPopupType
{
    BigWin
}

#endregion

#region Helper Classes for Conversion

/// <summary>
/// The single seam between server JSON and client types. Wire shapes change here and nowhere else,
/// so the view and controller layers never see a backend revision.
/// </summary>
public static class InitDataConverter
{
    // Used only if the init carries neither totalLines nor any lines. Total bet = selected bet x 20,
    // which matches every captured balance: each spin at bet 0.01 cost exactly 0.20.
    private const int FallbackBetMultiplier = 20;

    internal static GameConfig ConvertToGameConfig(InitData serverData)
    {
        var gameData = serverData?.gameData;
        var serverSymbols = serverData?.uiData?.paylines?.symbols;
        var features = serverData?.features;

        int reelCount = (gameData?.lines != null && gameData.lines.Count > 0 && gameData.lines[0] != null)
            ? gameData.lines[0].Count
            : 5;

        var config = new GameConfig
        {
            reelCount = reelCount,
            rowCount = 3,
            activeLine = ResolveBetMultiplier(gameData),
            paylines = ConvertPaylines(gameData?.lines, reelCount, 3),
            availableBets = gameData?.bets,
            symbols = new List<SymbolInfo>()
        };

        if (config.availableBets == null || config.availableBets.Count == 0)
        {
            UnityEngine.Debug.LogError("[InitDataConverter] Init carried no bet levels — the bet controls will do nothing.");
        }

        if (serverSymbols == null)
        {
            UnityEngine.Debug.LogError("[InitDataConverter] Init carried no symbol table — symbol art and paytables will be unresolved.");
            return config;
        }

        foreach (var serverSymbol in serverSymbols)
        {
            if (serverSymbol == null) continue;

            // The role comes from "group", never the name.
            string group = (serverSymbol.group ?? string.Empty).Trim().ToLowerInvariant();

            var multipliers = ConvertPaytable(serverSymbol, reelCount);

            var symbolInfo = new SymbolInfo
            {
                id = serverSymbol.id,
                name = serverSymbol.name,
                multipliers = multipliers,
                isWild = group == "wild",
                isScatter = group == "scatter",
                minMatch = multipliers.Count > 0 ? reelCount - multipliers.Count + 1 : 0
            };
            symbolInfo.isSpecial = symbolInfo.isWild || symbolInfo.isScatter;

            // An unknown group means the backend added a role this client has never heard of.
            if (group != "high" && group != "low" && !symbolInfo.isSpecial)
            {
                UnityEngine.Debug.LogError($"[InitDataConverter] Symbol id {symbolInfo.id} (name '{symbolInfo.name}') has unrecognised group '{serverSymbol.group}' — it will be treated as an ordinary symbol.");
            }

            config.symbols.Add(symbolInfo);

            if (symbolInfo.isWild) config.wildSymbolId = symbolInfo.id;
            if (symbolInfo.isScatter) config.scatterSymbolId = symbolInfo.id;
        }

        // The Wild comes from its group above; features.wild.symbolId names it too, so the two are
        // only cross-checked.
        int? featureWildId = features?.wild?.symbolId;
        if (featureWildId.HasValue && featureWildId.Value != config.wildSymbolId)
        {
            UnityEngine.Debug.LogError($"[InitDataConverter] features.wild names symbol {featureWildId.Value} but the symbol table's wild is {config.wildSymbolId} — using the symbol table's.");
        }

        config.freeSpinsSymbolId = ResolveFeatureSymbolId(features?.freeSpins?.symbolId, "freeSpins", config);
        config.ballBonusSymbolId = ResolveFeatureSymbolId(features?.ballPickemBonus?.symbolId, "ballPickemBonus", config);
        config.sandbellBonusSymbolId = ResolveFeatureSymbolId(features?.sandbellPickBonus?.symbolId, "sandbellPickBonus", config);

        return config;
    }

    // totalLines is the multiplier — confirmed against balances. The line table is the fallback, and
    // the two are checked against each other, since a mismatch means one of them is wrong.
    private static int ResolveBetMultiplier(ServerGameData gameData)
    {
        int lineCount = gameData?.lines?.Count ?? 0;

        if (gameData != null && gameData.totalLines > 0)
        {
            if (lineCount > 0 && lineCount != gameData.totalLines)
            {
                UnityEngine.Debug.LogError($"[InitDataConverter] Init says totalLines {gameData.totalLines} but sends {lineCount} lines — total bet uses totalLines.");
            }
            return gameData.totalLines;
        }

        int fallback = lineCount > 0 ? lineCount : FallbackBetMultiplier;
        UnityEngine.Debug.LogError($"[InitDataConverter] Init carried no totalLines — total bet assumed to be bet x {fallback}.");
        return fallback;
    }

    // Kept as sent, so a win's lineIndex still points at the right line. A malformed line is logged,
    // not dropped: dropping it would shift every line after it.
    private static List<List<int>> ConvertPaylines(List<List<int>> lines, int reelCount, int rowCount)
    {
        var paylines = lines ?? new List<List<int>>();

        if (paylines.Count == 0)
        {
            UnityEngine.Debug.LogError("[InitDataConverter] Init carried no paylines — line wins cannot be traced.");
            return paylines;
        }

        for (int i = 0; i < paylines.Count; i++)
        {
            var line = paylines[i];
            if (line == null || line.Count != reelCount || line.Exists(row => row < 0 || row >= rowCount))
            {
                UnityEngine.Debug.LogError($"[InitDataConverter] Payline {i} is not {reelCount} rows within 0..{rowCount - 1}: [{(line != null ? string.Join(",", line) : "null")}].");
            }
        }

        return paylines;
    }

    // The wire paytable ascends to a full-reel match; the client works descending from one, so it is
    // reversed. Counts that don't pay are zeros on the wire (A sends 0 for 2-of-a-kind) and are cut
    // off the end, so the list stops at the fewest that pay and nothing downstream shows a "pays 0" row.
    private static List<double> ConvertPaytable(ServerSymbolInfo serverSymbol, int reelCount)
    {
        var multipliers = new List<double>(serverSymbol.multiplier ?? new List<double>());

        if (multipliers.Count > reelCount)
        {
            UnityEngine.Debug.LogError($"[InitDataConverter] Symbol id {serverSymbol.id} sends {multipliers.Count} payouts for {reelCount} reels — the extra short counts are ignored.");
        }

        multipliers.Reverse();
        if (multipliers.Count > reelCount) multipliers.RemoveRange(reelCount, multipliers.Count - reelCount);
        while (multipliers.Count > 0 && multipliers[multipliers.Count - 1] <= 0) multipliers.RemoveAt(multipliers.Count - 1);

        return multipliers;
    }

    // A feature's symbol, checked against the symbol table. Missing or unknown reads as -1, never 0,
    // which is the Wild.
    private static int ResolveFeatureSymbolId(int? symbolId, string featureName, GameConfig config)
    {
        if (!symbolId.HasValue)
        {
            UnityEngine.Debug.LogError($"[InitDataConverter] features.{featureName} names no symbolId — its trigger symbol is unknown.");
            return -1;
        }

        SymbolInfo symbol = config.symbols.Find(s => s.id == symbolId.Value);
        if (symbol == null)
        {
            UnityEngine.Debug.LogError($"[InitDataConverter] features.{featureName} names symbol {symbolId.Value}, which is not in the symbol table.");
            return -1;
        }

        if (!symbol.isScatter)
        {
            UnityEngine.Debug.LogError($"[InitDataConverter] features.{featureName} names symbol {symbolId.Value} ('{symbol.name}'), which is not a scatter.");
        }

        return symbolId.Value;
    }

    internal static PlayerData ConvertToPlayerData(ServerPlayer serverPlayer, int defaultBetIndex = 0)
    {
        return new PlayerData
        {
            balance = serverPlayer != null ? serverPlayer.balance : 0,
            currentBetIndex = defaultBetIndex
        };
    }

    /// <summary>
    /// Converts one spin response into the client's SpinResult.
    /// betAmount is accepted for signature stability but no longer used: the server always sends
    /// the post-spin balance, and the fallback below works off the already-deducted local balance.
    /// </summary>
    internal static SpinResult ConvertServerResponseToSpinResult(ServerSpinResponse serverResponse, double currentBalance, double betAmount, GameConfig gameConfig)
    {
        var payload = serverResponse?.payload;

        // grandTotalWin is the spin's total and passes straight through. payload.winAmount is the line
        // wins alone, so it is kept separately and never used as the figure to show. Nothing is
        // summed here.
        double totalWin = payload?.grandTotalWin ?? 0;
        double newBalance = serverResponse?.player?.balance ?? CalculateNewBalance(currentBalance, totalWin);

        return new SpinResult
        {
            resultMatrix = ConvertMatrixToColumns(serverResponse?.matrix, gameConfig),
            winAmount = totalWin,
            lineWinAmount = payload?.winAmount ?? 0,
            winLines = ConvertLineWins(payload?.lineWins, gameConfig),

            playerData = new PlayerData
            {
                balance = newBalance,
                currentBetIndex = 0
            },

            freeGame = ConvertFreeGame(payload?.freeGames)
        };
    }

    // Never returns null, so the controller can read it without guarding every access. A missing
    // block is indistinguishable from "not in a round", which is the correct reading either way.
    //
    // spinsAwarded is left false: Gold of Luck derived it from its wheel, and the wire's
    // freeGames.triggered stays true for the whole round, so it cannot say which spin did the
    // awarding. Wild Jokers' own trigger flag goes here once it is captured.
    private static FreeGameData ConvertFreeGame(ServerFreeGamesResult serverFreeGames)
    {
        if (serverFreeGames == null) return new FreeGameData();

        return new FreeGameData
        {
            spinsRemaining = serverFreeGames.remaining,
            roundWin = serverFreeGames.totalFreeGamesWin
        };
    }

    // The one place a row and column become a flat index, so the bounds check cannot drift between
    // callers.
    private static bool TryGetFlatIndex(int row, int col, GameConfig gameConfig, string source, out int flatIndex)
    {
        flatIndex = -1;

        int reelCount = gameConfig != null ? gameConfig.reelCount : 5;
        int rowCount = gameConfig != null ? gameConfig.rowCount : 3;

        if (row < 0 || row >= rowCount || col < 0 || col >= reelCount)
        {
            UnityEngine.Debug.LogError($"[InitDataConverter] {source} is outside the {rowCount}x{reelCount} grid — cell skipped.");
            return false;
        }

        flatIndex = row * reelCount + col;
        return true;
    }

    // Server matrix is row-major (matrix[row][col]); the client works column-major ([reel][row]),
    // so this transposes. No padding rows to skip — every row the server sends is live.
    private static List<List<int>> ConvertMatrixToColumns(List<List<string>> serverMatrix, GameConfig gameConfig)
    {
        int reelCount = gameConfig != null ? gameConfig.reelCount : 5;
        int rowCount = gameConfig != null ? gameConfig.rowCount : 3;

        if (serverMatrix == null || serverMatrix.Count == 0)
        {
            UnityEngine.Debug.LogError("[InitDataConverter] Spin response carried no matrix.");
            return GenerateDefaultMatrix(reelCount, rowCount);
        }

        var matrix = new List<List<int>>();

        for (int col = 0; col < reelCount; col++)
        {
            var column = new List<int>();
            for (int row = 0; row < rowCount; row++)
            {
                if (row >= serverMatrix.Count || serverMatrix[row] == null || col >= serverMatrix[row].Count)
                {
                    UnityEngine.Debug.LogError($"[InitDataConverter] matrix has no cell at row {row}, col {col}.");
                    column.Add(0);
                    continue;
                }

                if (!int.TryParse(serverMatrix[row][col], out int symbolId))
                {
                    UnityEngine.Debug.LogError($"[InitDataConverter] Could not parse symbol at row {row}, col {col}.");
                    symbolId = 0;
                }

                column.Add(symbolId);
            }
            matrix.Add(column);
        }

        return matrix;
    }

    // One WinLine per winning payline. The server already says which cells pay and what is in them,
    // so nothing is rebuilt from the line table.
    private static List<WinLine> ConvertLineWins(List<ServerLineWin> lineWins, GameConfig gameConfig)
    {
        var winLines = new List<WinLine>();
        if (lineWins == null) return winLines;

        for (int i = 0; i < lineWins.Count; i++)
        {
            var lineWin = lineWins[i];
            if (lineWin == null) continue;

            var flatPositions = new List<int>();
            if (lineWin.matchedPositions != null)
            {
                foreach (var cell in lineWin.matchedPositions)
                {
                    if (cell == null) continue;

                    if (TryGetFlatIndex(cell.row, cell.col, gameConfig, $"lineWins[{i}] cell ({cell.row},{cell.col})", out int flatIndex))
                    {
                        flatPositions.Add(flatIndex);
                    }
                }
            }

            winLines.Add(new WinLine
            {
                lineId = lineWin.lineIndex,
                symbolId = GetPayingSymbolId(lineWin.symbolIds, gameConfig),
                positions = flatPositions,
                winAmount = lineWin.winInCash,
                matchCount = lineWin.matchLength,
                // A missing field deserializes as 0, which would read as "pays nothing"; treat it
                // as no multiplier. Never clamps a real value: every line is x1 or more.
                multiplier = Math.Max(1.0, lineWin.lineMultiplier)
            });
        }

        return winLines;
    }

    // The symbol a line pays for: the first matched symbol that isn't the Wild, since a Wild standing
    // in is listed as itself ([0,6,6] pays as 10s). A line of nothing but Wilds pays for the Wild.
    // -1 when the server listed no symbols.
    private static int GetPayingSymbolId(List<int> symbolIds, GameConfig gameConfig)
    {
        if (symbolIds == null || symbolIds.Count == 0) return -1;

        int wildId = gameConfig != null ? gameConfig.wildSymbolId : -1;
        foreach (int id in symbolIds)
        {
            if (id != wildId) return id;
        }

        return symbolIds[0];
    }

    private static List<List<int>> GenerateDefaultMatrix(int reelCount, int rowCount)
    {
        var matrix = new List<List<int>>();
        for (int col = 0; col < reelCount; col++)
        {
            var column = new List<int>();
            for (int row = 0; row < rowCount; row++)
            {
                column.Add(0);
            }
            matrix.Add(column);
        }
        return matrix;
    }

    // currentBalance already reflects StartSpin()'s optimistic upfront deduction of the total bet,
    // so only the win is added back. Only reached if the server omits a balance.
    private static double CalculateNewBalance(double currentBalance, double winAmount)
    {
        return currentBalance + winAmount;
    }
}

#endregion
