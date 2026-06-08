import os
import csv
import re

DIR = r"c:\Users\Duran\.gemini\antigravity\scratch\fnpp"
CSV_DIR = os.path.join(DIR, "tmp_csv")

def update_albo():
    csv_path = os.path.join(CSV_DIR, "02-05-2026Albo d'oro.csv")
    html_path = os.path.join(DIR, "albo.html")
    with open(csv_path, 'r', encoding='utf-8') as f:
        reader = csv.reader(f)
        lines = list(reader)
    
    xiv_row = None
    for row in lines:
        if row and row[0] == 'XIV':
            xiv_row = row
            break
            
    if xiv_row:
        # oro, argento, bronzo, bronzo, maglia nera 1, maglia nera 2, partecipanti
        # row: XIV, 02/05/2026, Paolo P., Mauro, Alessio C., Federico, Giuseppe D'A., Christian, 39
        maglia_nera = f"{xiv_row[6]} / {xiv_row[7]}" if xiv_row[7] else xiv_row[6]
        
        new_tr = f'''        <tr>
          <td>{xiv_row[0]}</td>
          <td>{xiv_row[1]}</td>
          <td>{xiv_row[2]}</td>
          <td>{xiv_row[3]}</td>
          <td>{xiv_row[4]}</td>
          <td>{xiv_row[5]}</td>
          <td>{maglia_nera}</td>
          <td>{xiv_row[8]}</td>
        </tr>'''
        
        with open(html_path, 'r', encoding='utf-8') as f:
            content = f.read()
            
        # find the end of the FNPP Cup table
        # We look for XIII row, then add XIV after it
        match = re.search(r'(<tr>\s*<td>XIII</td>.*?</tr>)', content, re.DOTALL)
        if match:
            new_content = content[:match.end()] + "\n" + new_tr + content[match.end():]
            with open(html_path, 'w', encoding='utf-8') as f:
                f.write(new_content)

def update_campione():
    csv_path = os.path.join(CSV_DIR, "02-05-2026Campione non ufficiale.csv")
    html_path = os.path.join(DIR, "campione_non_ufficiale.html")
    
    with open(csv_path, 'r', encoding='utf-8') as f:
        reader = csv.reader(f)
        lines = list(reader)[1:] # skip header
        
    tbody_html = []
    
    current_ed = None
    for row in lines:
        if not row or not any(row): continue
        if len(row) < 5: continue
        ed = row[0].strip()
        if not ed and current_ed:
            ed = current_ed
        if ed:
            current_ed = ed
        
        date = row[1].strip()
        defender = row[2].strip()
        if not defender: defender = "-"
        
        challenger = row[3].strip()
        res = row[4].strip()
        
        # fix the "Finale" thing for XIV
        if ed == 'XIV' and defender in ['Finale', 'Semifinale', 'Quarti di finale', 'Ottavi di finale', 'Campionato']:
            defender = 'Paolo P.'
            
        # Replace newlines in challenger with " vs "
        challenger = challenger.replace('\n', ' vs ')
        
        # Check if defender or challenger won based on result (e.g. 3-0, 0-3)
        # Assuming the first number is the defender score and second is challenger score
        m = re.match(r'(\d+)-(\d+)', res)
        if m:
            d_score = int(m.group(1))
            c_score = int(m.group(2))
            if d_score > c_score:
                res_span = f'<span class="defender-win">{d_score}-{c_score}</span>'
            else:
                res_span = f'<span class="challenger-win">{d_score}-{c_score}</span>'
            
            rest = res[m.end():].strip()
            if rest:
                res_html = f'{res_span} {rest}'
            else:
                res_html = res_span
        else:
            res_html = res
            
        tr = f'          <tr><td>{ed}</td><td>{date}</td><td>{defender}</td><td>{challenger}</td><td>{res_html}</td></tr>'
        tbody_html.append(tr)
        
    tbody_str = "\n".join(tbody_html)
    
    with open(html_path, 'r', encoding='utf-8') as f:
        content = f.read()
        
    new_content = re.sub(r'<tbody>.*?</tbody>', f'<tbody>\n{tbody_str}\n        </tbody>', content, flags=re.DOTALL)
    with open(html_path, 'w', encoding='utf-8') as f:
        f.write(new_content)

def update_ranking():
    csv_path = os.path.join(CSV_DIR, "02-05-2026Classifica FNPP.csv")
    html_path = os.path.join(DIR, "ranking.html")
    
    with open(csv_path, 'r', encoding='utf-8') as f:
        reader = csv.reader(f)
        lines = list(reader)[2:] # skip headers
        
    tbody_html = []
    
    for row in lines:
        if not row or not row[0]: continue
        pos = row[0].strip()
        giocatore = row[1].strip()
        punti = row[2].strip()
        atp = row[3].strip()
        ind_aff = row[6].strip()
        
        if not giocatore: continue
        
        tr = f'''  <tr>
    <td>{pos}</td>
    <td>{giocatore}</td>
    <td>{punti}</td>
    <td>{atp}</td>
    <td>{ind_aff}</td>
  </tr>'''
        tbody_html.append(tr)
        
    tbody_str = "\n".join(tbody_html)
    
    with open(html_path, 'r', encoding='utf-8') as f:
        content = f.read()
        
    new_content = re.sub(r'<tbody id="rankingBody">.*?</tbody>', f'<tbody id="rankingBody">\n{tbody_str}\n      </tbody>', content, flags=re.DOTALL)
    with open(html_path, 'w', encoding='utf-8') as f:
        f.write(new_content)


def update_xiv():
    csv_path = os.path.join(CSV_DIR, "XIV edizione.csv")
    html_path = os.path.join(DIR, "FNPPcupXIV.html")
    
    with open(csv_path, 'r', encoding='utf-8') as f:
        reader = csv.reader(f)
        lines = list(reader)
        
    # Read the data from CSV
    playoff_matches = []
    campionato_rows = []
    campionato_matches_1 = []
    campionato_matches_2 = []
    campionato_matches_3 = []
    ottavi = []
    quarti = []
    semifinali = []
    finale = []
    
    state = None
    for row in lines:
        if not row: continue
        if "Playoff" in row[0]: state = "playoff"; continue
        elif "Fase campionato" in row[0]: state = "campionato_standings"; continue
        elif "1^ giornata" in row[0] and state in ["campionato_standings", "campionato_match1"]: state = "campionato_match1"; continue
        elif "2^ giornata" in row[0] and state in ["campionato_match1", "campionato_match2"]: state = "campionato_match2"; continue
        elif "3^ giornata" in row[0] and state in ["campionato_match2", "campionato_match3"]: state = "campionato_match3"; continue
        elif "Ottavi di finale" in row[0]: state = "ottavi"; continue
        elif "Quarti di finale" in row[0]: state = "quarti"; continue
        elif "Semifinali" in row[0]: state = "semifinali"; continue
        elif "Finale" in row[0]: state = "finale"; continue
        
        if not row[0] and state != "campionato_standings": continue
        
        if state == "playoff":
            if row[0]: playoff_matches.append(row)
        elif state == "campionato_standings":
            if row[0] and row[0].isdigit(): campionato_rows.append(row)
        elif state == "campionato_match1":
            if row[0]: campionato_matches_1.append(row)
        elif state == "campionato_match2":
            if row[0]: campionato_matches_2.append(row)
        elif state == "campionato_match3":
            if row[0]: campionato_matches_3.append(row)
        elif state == "ottavi":
            if row[0]: ottavi.append(row)
        elif state == "quarti":
            if row[0]: quarti.append(row)
        elif state == "semifinali":
            if row[0]: semifinali.append(row)
        elif state == "finale":
            if row[0]: finale.append(row)

    with open(html_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Update Playoff Matches
    playoff_html = []
    for m in playoff_matches:
        p1 = m[0]
        p2 = m[1]
        score_full = m[2]
        if " " in score_full:
            score, sets = score_full.split(" ", 1)
        else:
            score, sets = score_full, ""
        playoff_html.append(f'<div class="match-item">{p1} - {p2} <span class="score">{score}</span> <span class="sets">{sets}</span></div>')
    
    if playoff_html:
        content = re.sub(r'(<!-- Playoff -->.*?<div class="matches-container">).*?(</div>)', r'\1\n      ' + '\n      '.join(playoff_html) + r'\n    \2', content, flags=re.DOTALL)

    # 2. Update Campionato Table
    campionato_table = []
    for row in campionato_rows:
        pos = row[0]
        gioc = row[1]
        punti = row[2]
        diff_p = row[3]
        diff_s = row[4]
        diff_pt = row[5]
        
        bg = ""
        if int(pos) <= 16:
            bg = ' style="background-color: rgba(68, 106, 201, 0.08);"'
        elif int(pos) >= 33:
            bg = ' style="background-color: rgba(201, 18, 18, 0.08);"'
            
        campionato_table.append(f'<tr{bg}><td>{pos}</td><td>{gioc}</td><td>{punti}</td><td>{diff_p}</td><td>{diff_s}</td><td>{diff_pt}</td></tr>')
        
    campionato_table_str = "\n        ".join(campionato_table)
    # find Fase Campionato table
    content = re.sub(r'(<!-- Fase Campionato -->.*?<th>Diff. punti</th>\s*</tr>).*?(</table>)', r'\1\n        ' + campionato_table_str + r'\n      \2', content, flags=re.DOTALL)

    def format_matches(matches, day_title, top_margin):
        html = [f'<h4 style="color: #777; font-size: 0.9rem; text-transform: uppercase; margin: {top_margin}px 0 0 2px;">{day_title}</h4>']
        for m in matches:
            p1 = m[0]
            p2 = m[1]
            score_full = m[2] if len(m) > 2 else "TBD"
            if score_full and score_full != "TBD" and " " in score_full:
                score, sets = score_full.split(" ", 1)
            else:
                score, sets = score_full, ""
            html.append(f'<div class="match-item">{p1} - {p2} <span class="score">{score}</span> <span class="sets">{sets}</span></div>')
        return "\n      ".join(html)

    # 3. Update Campionato Matches
    g1 = format_matches(campionato_matches_1, "1ª giornata", 10)
    g2 = format_matches(campionato_matches_2, "2ª giornata", 15)
    g3 = format_matches(campionato_matches_3, "3ª giornata", 15)
    
    all_g = g1 + "\n\n      " + g2 + "\n\n      " + g3
    
    # We replace from the first <h4 ...>1ª giornata</h4> until the end of that matches-container
    content = re.sub(r'(<h4 style="color: #777; font-size: 0.9rem; text-transform: uppercase; margin: 10px 0 0 2px;">1ª giornata</h4>.*?)</div>\s*<!-- Ottavi di finale -->', all_g + '\n    </div>\n\n    <!-- Ottavi di finale -->', content, flags=re.DOTALL)
    
    # 4. Ottavi
    ottavi_html = []
    for m in ottavi:
        p1, p2 = m[0], m[1]
        sf = m[2]
        if " " in sf: s, sets = sf.split(" ", 1)
        else: s, sets = sf, ""
        ottavi_html.append(f'<div class="match-item">{p1} - {p2} <span class="score">{s}</span> <span class="sets">{sets}</span></div>')
    content = re.sub(r'(<!-- Ottavi di finale -->.*?<div class="matches-container">).*?(</div>)', r'\1\n      ' + '\n      '.join(ottavi_html) + r'\n    \2', content, flags=re.DOTALL)

    # 5. Quarti
    quarti_html = []
    for m in quarti:
        p1, p2 = m[0], m[1]
        sf = m[2]
        if " " in sf: s, sets = sf.split(" ", 1)
        else: s, sets = sf, ""
        quarti_html.append(f'<div class="match-item">{p1} - {p2} <span class="score">{s}</span> <span class="sets">{sets}</span></div>')
    content = re.sub(r'(<!-- Quarti di finale -->.*?<div class="matches-container">).*?(</div>)', r'\1\n      ' + '\n      '.join(quarti_html) + r'\n    \2', content, flags=re.DOTALL)

    # 6. Semifinali
    semifinali_html = []
    for m in semifinali:
        p1, p2 = m[0], m[1]
        sf = m[2]
        if " " in sf: s, sets = sf.split(" ", 1)
        else: s, sets = sf, ""
        semifinali_html.append(f'<div class="match-item">{p1} - {p2} <span class="score">{s}</span> <span class="sets">{sets}</span></div>')
    content = re.sub(r'(<!-- Semifinali -->.*?<div class="matches-container">).*?(</div>)', r'\1\n      ' + '\n      '.join(semifinali_html) + r'\n    \2', content, flags=re.DOTALL)

    # 7. Finale
    finale_html = []
    for m in finale:
        p1, p2 = m[0], m[1]
        sf = m[2]
        if " " in sf: s, sets = sf.split(" ", 1)
        else: s, sets = sf, ""
        finale_html.append(f'<div class="match-item">{p1} - {p2} <span class="score">{s}</span> <span class="sets">{sets}</span></div>')
    content = re.sub(r'(<!-- Finale -->.*?<div class="matches-container">).*?(</div>)', r'\1\n      ' + '\n      '.join(finale_html) + r'\n    \2', content, flags=re.DOTALL)

    with open(html_path, 'w', encoding='utf-8') as f:
        f.write(content)

if __name__ == "__main__":
    update_albo()
    update_campione()
    update_ranking()
    update_xiv()
    print("Done")
