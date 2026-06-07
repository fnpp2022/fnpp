using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FNPPProfiles
{
    class PlayerData
    {
        public string Name { get; set; }
        public string BestResult { get; set; }
        public string Participations { get; set; }
        public string OtherTournaments { get; set; }
        
        public int MatchesPlayed { get; set; }
        public int MatchesWon { get; set; }
        public int MatchesLost { get; set; }
        
        public int DiffPartite { get; set; }
        public int DiffSet { get; set; }
        public int DiffPunti { get; set; }

        public string RankingPos { get; set; }
        public string Points { get; set; }

        private List<MatchData> _matches;
        public List<MatchData> Matches { get { return _matches; } }

        private List<string> _palmares;
        public List<string> Palmares { get { return _palmares; } }

        public PlayerData()
        {
            RankingPos = "N.C.";
            Points = "0";
            _matches = new List<MatchData>();
            _palmares = new List<string>();
        }
    }

    class MatchData
    {
        public string Edition { get; set; }
        public string Date { get; set; }
        public string Phase { get; set; }
        public string Opponent { get; set; }
        public string ResultString { get; set; }
        
        public bool IsWin { get; set; }
        public string SetsDetail { get; set; }
        public string SimpleScore { get; set; }
    }

    class RankData 
    {
        public string pos;
        public string pts;
    }

    class Program
    {
        static void Main(string[] args)
        {
            string baseDir = @"c:\Users\Duran\.gemini\antigravity\scratch\fnpp";
            string csvDir = Path.Combine(baseDir, "tmp_csv_excel");
            string rankingPath = Path.Combine(baseDir, "ranking.html");
            string profiliPath = Path.Combine(baseDir, "profili.html");

            // Use Default Encoding to parse Excel CSV (ANSI/Windows-1252) which contains ò, à, etc.
            Encoding csvEncoding = Encoding.Default;

            // 1. Parse ranking.html
            var rankingDict = new Dictionary<string, RankData>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(rankingPath))
            {
                string rankingHtml = File.ReadAllText(rankingPath, Encoding.UTF8); // ranking.html is UTF-8
                var matchBody = Regex.Match(rankingHtml, @"<tbody id=""rankingBody"">(.*?)</tbody>", RegexOptions.Singleline);
                if (matchBody.Success)
                {
                    var rowMatches = Regex.Matches(matchBody.Groups[1].Value, @"<tr><td>(.*?)</td><td>(.*?)</td><td>(.*?)</td>");
                    foreach (Match m in rowMatches)
                    {
                        string pos = m.Groups[1].Value.Trim();
                        string name = m.Groups[2].Value.Trim();
                        string pts = m.Groups[3].Value.Trim();
                        rankingDict[name] = new RankData { pos = pos, pts = pts };
                    }
                }
            }

            var players = new List<PlayerData>();
            var csvFiles = Directory.GetFiles(csvDir, "*.csv");

            foreach (var file in csvFiles)
            {
                // We should decode the filename as well since GetFiles returns default system charset
                string filename = Path.GetFileNameWithoutExtension(file).Trim();
                if (string.Equals(filename, "VUOTO", StringComparison.OrdinalIgnoreCase)) continue;

                var player = new PlayerData { Name = filename };

                // Get Ranking
                if (rankingDict.ContainsKey(player.Name))
                {
                    player.RankingPos = rankingDict[player.Name].pos;
                    player.Points = rankingDict[player.Name].pts;
                }
                else
                {
                    // Try to handle variants
                    string keyMatch = null;
                    foreach (var k in rankingDict.Keys)
                    {
                        if (k.Contains(player.Name) || player.Name.Contains(k)) 
                        {
                            keyMatch = k;
                            break;
                        }
                    }
                    if (keyMatch != null)
                    {
                        player.RankingPos = rankingDict[keyMatch].pos;
                        player.Points = rankingDict[keyMatch].pts;
                    }
                }

                ParseCSV(file, player, csvEncoding);
                players.Add(player);

                // Generate HTML
                GenerateProfileHtml(baseDir, player);
            }

            // Generate profili.html
            GenerateProfiliHtml(profiliPath, players);

            Console.WriteLine("Done! Processed " + players.Count + " players.");
        }

        static void ParseCSV(string file, PlayerData player, Encoding encoding)
        {
            var lines = File.ReadAllLines(file, encoding);
            
            bool parsingPalmares = false;

            // Look for summary stats
            foreach (var line in lines)
            {
                var cols = SplitCsv(line);
                for (int i = 0; i < cols.Count; i++)
                {
                    string colTrimmed = cols[i].Trim();
                    
                    if (i < cols.Count - 1)
                    {
                        if (string.Equals(colTrimmed, "Miglior risultato", StringComparison.OrdinalIgnoreCase))
                            player.BestResult = cols[i + 1].Trim();
                        else if (string.Equals(colTrimmed, "Partecipazioni", StringComparison.OrdinalIgnoreCase))
                            player.Participations = cols[i + 1].Trim();
                        else if (string.Equals(colTrimmed, "Altri tornei", StringComparison.OrdinalIgnoreCase))
                            player.OtherTournaments = cols[i + 1].Trim();
                    }

                    // Palmares logic (usually in column 8)
                    if (colTrimmed.StartsWith("Palmar", StringComparison.OrdinalIgnoreCase))
                    {
                        parsingPalmares = true;
                        continue;
                    }

                    if (parsingPalmares && i == 7) // Col H is 7, I is 8 (0-indexed). Wait, "Palmarès" is usually in I (8)
                    {
                        // In CSV: H is 7, I is 8. Let's just check if it's not empty and parsingPalmares is true.
                        // Actually, if we hit the row with Palmars, subsequent rows might have the title in col I and count in J
                    }
                }
            }

            // Let's refine Palmares parsing based on column index
            int palmaresCol = -1;
            for (int r = 0; r < lines.Length; r++)
            {
                var cols = SplitCsv(lines[r]);
                for (int c = 0; c < cols.Count; c++)
                {
                    if (cols[c].Trim().StartsWith("Palmar", StringComparison.OrdinalIgnoreCase))
                    {
                        palmaresCol = c;
                        string currentContext = "";
                        // Read down from this cell
                        for (int pr = r + 1; pr < lines.Length; pr++)
                        {
                            var pCols = SplitCsv(lines[pr]);
                            if (pCols.Count > palmaresCol && !string.IsNullOrWhiteSpace(pCols[palmaresCol]))
                            {
                                string title = pCols[palmaresCol].Trim();
                                string count = (pCols.Count > palmaresCol + 1) ? pCols[palmaresCol + 1].Trim() : "";
                                if (!string.IsNullOrEmpty(count))
                                {
                                    if (!string.IsNullOrEmpty(currentContext))
                                    {
                                        player.Palmares.Add(string.Format("{0} ({1}): {2}", title, currentContext, count));
                                    }
                                    else
                                    {
                                        player.Palmares.Add(string.Format("{0}: {1}", title, count));
                                    }
                                }
                                else
                                {
                                    currentContext = title;
                                }
                            }
                        }
                        break;
                    }
                }
                if (palmaresCol != -1) break;
            }

            // Parse matches
            bool isFirstLine = true;
            foreach (var line in lines)
            {
                if (isFirstLine)
                {
                    isFirstLine = false;
                    continue;
                }
                
                var cols = SplitCsv(line);
                if (cols.Count < 5) continue;

                string ed = cols[0].Trim();
                string date = cols[1].Trim();
                string phase = cols[2].Trim();
                string opp = cols[3].Trim();
                string res = cols[4].Trim();

                if (string.IsNullOrEmpty(ed) || string.IsNullOrEmpty(date)) continue;

                var mData = new MatchData
                {
                    Edition = ed,
                    Date = date,
                    Phase = phase,
                    Opponent = opp,
                    ResultString = res
                };

                ParseMatchResult(mData, player);
                player.Matches.Add(mData);
            }
        }

        static void ParseMatchResult(MatchData m, PlayerData p)
        {
            string s = m.ResultString;
            
            var match = Regex.Match(s, @"^(\d+)\s*-\s*(\d+)\s*(.*)");
            if (match.Success)
            {
                int wonSets = int.Parse(match.Groups[1].Value);
                int lostSets = int.Parse(match.Groups[2].Value);
                
                m.SimpleScore = string.Format("{0}-{1}", wonSets, lostSets);
                m.SetsDetail = match.Groups[3].Value.Trim();
                m.IsWin = wonSets > lostSets;

                p.MatchesPlayed++;
                if (m.IsWin) p.MatchesWon++;
                else p.MatchesLost++;

                p.DiffPartite += (m.IsWin ? 1 : -1);
                p.DiffSet += (wonSets - lostSets);

                if (string.Equals(m.SetsDetail, "AT", StringComparison.OrdinalIgnoreCase))
                {
                    p.DiffPunti += (wonSets - lostSets) * 11;
                }
                else
                {
                    var ptsMatch = Regex.Matches(m.SetsDetail, @"(\d+)\s*-\s*(\d+)");
                    foreach (Match ptM in ptsMatch)
                    {
                        int ptsWon = int.Parse(ptM.Groups[1].Value);
                        int ptsLost = int.Parse(ptM.Groups[2].Value);
                        p.DiffPunti += (ptsWon - ptsLost);
                    }
                }
            }
            else
            {
                m.SimpleScore = s;
                m.IsWin = false; 
            }
        }

        static void GenerateProfileHtml(string baseDir, PlayerData p)
        {
            string htmlPath = Path.Combine(baseDir, p.Name + ".html");

            string posDisplay = p.RankingPos + (p.RankingPos != "N.C." && p.RankingPos != "LN" ? "° Posto" : "");
            
            string bestResFormatted = p.BestResult;
            if (!string.IsNullOrEmpty(p.BestResult))
            {
                bestResFormatted = bestResFormatted.Replace("Argento", "<span class=\"medal-argento\">Argento</span>");
                bestResFormatted = bestResFormatted.Replace("Bronzo", "<span class=\"medal-bronzo\">Bronzo</span>");
                bestResFormatted = bestResFormatted.Replace("Oro", "<span class=\"medal-oro\">Oro</span>");
                
                bestResFormatted = string.Format("<span style=\"font-size: 1.2rem;\">{0}</span>", bestResFormatted);
            }

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"it\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine(string.Format("  <title>{0} - Profilo Atleta - FNPP</title>", p.Name));
            sb.AppendLine("  <link rel=\"stylesheet\" href=\"style.css\">");
            sb.AppendLine("  <style>");
            sb.AppendLine("    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 20px; margin-bottom: 40px; }");
            sb.AppendLine("    .stat-card { background: var(--color-bg-light); padding: 20px; border-radius: 12px; text-align: center; border: 1px solid rgba(68, 106, 201, 0.1); }");
            sb.AppendLine("    .stat-value { font-size: 2rem; font-weight: 800; color: var(--color-secondary); display: block; }");
            sb.AppendLine("    .stat-label { font-size: 0.9rem; color: #666; text-transform: uppercase; letter-spacing: 1px; }");
            sb.AppendLine("    .result-win { color: #446ac9; font-weight: bold; }");
            sb.AppendLine("    .result-loss { color: #e74c3c; font-weight: bold; }");
            sb.AppendLine("    .set-scores { font-weight: normal; display: inline-block; margin-left: 10px; color: var(--color-text); font-size: 1rem; }");
            sb.AppendLine("    .palmares-list { margin: 10px 0 0 0; padding-left: 20px; color: #444; }");
            sb.AppendLine("    .palmares-list li { margin-bottom: 5px; }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            sb.AppendLine("  <header class=\"page-header\">");
            sb.AppendLine("    <div class=\"container\">");
            sb.AppendLine(string.Format("      <h1>{0}</h1>", p.Name));
            if (!string.IsNullOrEmpty(bestResFormatted))
            {
                sb.AppendLine(string.Format("      <p style=\"margin-top: 10px; font-size: 1.2rem;\">{0}</p>", bestResFormatted));
            }
            sb.AppendLine("      <a href=\"profili.html\" class=\"back-link\" style=\"margin-top: 20px;\">⬅ Torna ai Profili</a>");
            sb.AppendLine("    </div>");
            sb.AppendLine("  </header>");

            sb.AppendLine("  <main class=\"page-content container\">");
            sb.AppendLine("    <section>");
            sb.AppendLine("      <h2 style=\"margin-bottom: 25px;\">Statistiche Carriera</h2>");
            sb.AppendLine("      <div class=\"stats-grid\">");
            sb.AppendLine("        <div class=\"stat-card\">");
            sb.AppendLine(string.Format("          <span class=\"stat-value\">{0}</span>", p.Participations ?? "0"));
            sb.AppendLine("          <span class=\"stat-label\">Partecipazioni</span>");
            sb.AppendLine("        </div>");
            sb.AppendLine("        <div class=\"stat-card\">");
            sb.AppendLine(string.Format("          <span class=\"stat-value\">{0}{1}</span>", (p.DiffPartite > 0 ? "+" : ""), p.DiffPartite));
            sb.AppendLine("          <span class=\"stat-label\">Diff. Partite</span>");
            sb.AppendLine("        </div>");
            sb.AppendLine("        <div class=\"stat-card\">");
            sb.AppendLine(string.Format("          <span class=\"stat-value\">{0}{1}</span>", (p.DiffSet > 0 ? "+" : ""), p.DiffSet));
            sb.AppendLine("          <span class=\"stat-label\">Diff. Set</span>");
            sb.AppendLine("        </div>");
            sb.AppendLine("        <div class=\"stat-card\">");
            sb.AppendLine(string.Format("          <span class=\"stat-value\">{0}{1}</span>", (p.DiffPunti > 0 ? "+" : ""), p.DiffPunti));
            sb.AppendLine("          <span class=\"stat-label\">Diff. Punti</span>");
            sb.AppendLine("        </div>");
            sb.AppendLine("      </div>");

            sb.AppendLine("      <div style=\"background: white; padding: 20px; border-radius: 12px; border: 1px solid rgba(68, 106, 201, 0.1); margin-bottom: 30px; display: flex; justify-content: center; gap: 40px; align-items: center; box-shadow: 0 4px 12px rgba(0,0,0,0.03);\">");
            sb.AppendLine("        <div style=\"text-align: center;\">");
            sb.AppendLine("          <span style=\"display: block; font-size: 0.8rem; color: #777; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 5px;\">Ranking Attuale</span>");
            sb.AppendLine(string.Format("          <span style=\"font-size: 1.5rem; font-weight: 800; color: var(--color-secondary);\">{0}</span>", posDisplay));
            sb.AppendLine("        </div>");
            sb.AppendLine("        <div style=\"width: 1px; height: 40px; background: rgba(0,0,0,0.1);\"></div>");
            sb.AppendLine("        <div style=\"text-align: center;\">");
            sb.AppendLine("          <span style=\"display: block; font-size: 0.8rem; color: #777; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 5px;\">Punti Totali</span>");
            sb.AppendLine(string.Format("          <span style=\"font-size: 1.5rem; font-weight: 800; color: var(--color-secondary);\">{0}</span>", p.Points));
            sb.AppendLine("        </div>");
            sb.AppendLine("      </div>");

            bool hasOtherTournaments = !string.IsNullOrEmpty(p.OtherTournaments) && p.OtherTournaments != "0" && p.OtherTournaments != "Nessuno" && p.OtherTournaments != "-";
            bool hasPalmares = p.Palmares.Count > 0;

            if (hasOtherTournaments || hasPalmares)
            {
                sb.AppendLine("      <div style=\"display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 20px; margin-bottom: 30px;\">");
                
                if (hasOtherTournaments)
                {
                    sb.AppendLine("        <div style=\"padding: 20px; background: rgba(68, 106, 201, 0.05); border-radius: 12px; border: 1px solid rgba(68, 106, 201, 0.1);\">");
                    sb.AppendLine("          <h3 style=\"color: var(--color-secondary); margin-bottom: 10px; font-size: 1.1rem;\">Altre competizioni</h3>");
                    sb.AppendLine(string.Format("          <p style=\"margin: 0; color: #444;\">{0}</p>", p.OtherTournaments));
                    sb.AppendLine("        </div>");
                }

                if (hasPalmares)
                {
                    sb.AppendLine("        <div style=\"padding: 20px; background: rgba(255, 215, 0, 0.05); border-radius: 12px; border: 1px solid rgba(255, 215, 0, 0.3);\">");
                    sb.AppendLine("          <h3 style=\"color: var(--color-secondary); margin-bottom: 10px; font-size: 1.1rem;\">Palmarès</h3>");
                    sb.AppendLine("          <ul class=\"palmares-list\">");
                    foreach (var pal in p.Palmares)
                    {
                        string liFormatted = pal;
                        liFormatted = liFormatted.Replace("Argento", "<span class=\"medal-argento\">Argento</span>");
                        liFormatted = liFormatted.Replace("Bronzo", "<span class=\"medal-bronzo\">Bronzo</span>");
                        liFormatted = liFormatted.Replace("Oro", "<span class=\"medal-oro\">Oro</span>");
                        sb.AppendLine(string.Format("            <li>{0}</li>", liFormatted));
                    }
                    sb.AppendLine("          </ul>");
                    sb.AppendLine("        </div>");
                }

                sb.AppendLine("      </div>");
            }

            sb.AppendLine("    </section>");

            sb.AppendLine("    <section>");
            sb.AppendLine("      <h2 style=\"margin-bottom: 25px;\">Cronologia Match</h2>");
            sb.AppendLine("      <div class=\"table-container\">");
            sb.AppendLine("        <table>");
            sb.AppendLine("          <thead>");
            sb.AppendLine("            <tr>");
            sb.AppendLine("              <th>Edizione</th>");
            sb.AppendLine("              <th>Data</th>");
            sb.AppendLine("              <th>Fase</th>");
            sb.AppendLine("              <th>Avversario</th>");
            sb.AppendLine("              <th>Risultato</th>");
            sb.AppendLine("            </tr>");
            sb.AppendLine("          </thead>");
            sb.AppendLine("          <tbody>");

            string currentEdition = "";
            foreach (var m in p.Matches)
            {
                if (m.Edition != currentEdition && !string.IsNullOrEmpty(m.Edition))
                {
                    currentEdition = m.Edition;
                    sb.AppendLine(string.Format("            <!-- {0} -->", currentEdition));
                }
                
                string resClass = m.IsWin ? "result-win" : "result-loss";
                string setsHtml = !string.IsNullOrEmpty(m.SetsDetail) ? string.Format(" <span class=\"set-scores\">{0}</span>", m.SetsDetail) : "";
                string styleAttr = (m.Edition.Contains("Finals") || m.Edition.Contains("Special")) ? " style=\"background-color: rgba(0,0,0,0.02);\"" : "";

                sb.AppendLine(string.Format("            <tr{0}>", styleAttr));
                sb.AppendLine(string.Format("              <td>{0}</td>", m.Edition));
                sb.AppendLine(string.Format("              <td>{0}</td>", m.Date));
                sb.AppendLine(string.Format("              <td>{0}</td>", m.Phase));
                sb.AppendLine(string.Format("              <td>{0}</td>", m.Opponent));
                sb.AppendLine(string.Format("              <td class=\"{0}\">{1}{2}</td>", resClass, m.SimpleScore, setsHtml));
                sb.AppendLine("            </tr>");
            }

            sb.AppendLine("          </tbody>");
            sb.AppendLine("        </table>");
            sb.AppendLine("      </div>");
            sb.AppendLine("    </section>");
            sb.AppendLine("  </main>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            File.WriteAllText(htmlPath, sb.ToString(), Encoding.UTF8); // Generate HTML in standard UTF-8
        }

        static void GenerateProfiliHtml(string path, List<PlayerData> players)
        {
            players.Sort((a, b) => a.Name.CompareTo(b.Name));

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"it\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("  <title>Profili Giocatori - Federazione Nocese Ping Pong</title>");
            sb.AppendLine("  <link rel=\"stylesheet\" href=\"style.css\">");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            sb.AppendLine("  <header class=\"page-header\">");
            sb.AppendLine("    <div class=\"container\">");
            sb.AppendLine("      <h1>Profili Giocatori</h1>");
            sb.AppendLine("      <a href=\"index.html\" class=\"back-link\">⬅ Torna alla Home</a>");
            sb.AppendLine("    </div>");
            sb.AppendLine("  </header>");

            sb.AppendLine("  <main class=\"page-content container\">");
            sb.AppendLine("    <p style=\"text-align: center; margin-bottom: 40px; color: #666; font-size: 1.1rem;\">");
            sb.AppendLine("      Ecco i protagonisti della Federazione Nocese Ping Pong. Sotto il nome di ogni atleta il suo miglior risultato nella FNPP Cup.");
            sb.AppendLine("    </p>");

            sb.AppendLine("    <div class=\"player-grid\">");

            foreach (var p in players)
            {
                string bestRes = string.IsNullOrEmpty(p.BestResult) ? "Nuovo giocatore" : p.BestResult;
                bestRes = bestRes.Replace("Argento", "<span class=\"medal-argento\">Argento</span>");
                bestRes = bestRes.Replace("Bronzo", "<span class=\"medal-bronzo\">Bronzo</span>");
                bestRes = bestRes.Replace("Oro", "<span class=\"medal-oro\">Oro</span>");
                
                var splitIdx = bestRes.IndexOf('(');
                if (splitIdx > 0)
                {
                    bestRes = bestRes.Insert(splitIdx, "<br>");
                }

                sb.AppendLine("      <div class=\"player-card\">");
                sb.AppendLine(string.Format("        <h3><a href=\"{0}.html\" style=\"text-decoration:none; color:inherit;\">{0}</a></h3>", p.Name));
                sb.AppendLine(string.Format("        <p>{0}</p>", bestRes));
                sb.AppendLine("      </div>");
            }

            sb.AppendLine("    </div>");
            sb.AppendLine("  </main>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8); // Generate index in UTF-8
        }

        static List<string> SplitCsv(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString());
            return result;
        }
    }
}
