import os
import re

phases_priority = {
    'Oro': 100,
    'Argento': 90,
    'Bronzo': 80,
    'Quarti di finale': 70,
    'Ottavi di finale': 60,
    'Ripescaggio': 50,
    'Spareggio': 50,
    'Spareggi': 50,
    'Campionato': 40,
    'Fase a gironi': 40,
    'Gironi': 40,
}

normalization = {
    'Spareggio': 'Ripescaggio',
    'Spareggi': 'Ripescaggio',
    'Campionato': 'Fase a gironi',
    'Gironi': 'Fase a gironi',
}

def get_best_results(directory):
    players = {}
    
    # 1. Check Albo d'oro for Medals
    albo_path = os.path.join(directory, 'albo.html')
    if os.path.exists(albo_path):
        with open(albo_path, 'r', encoding='utf-8') as f:
            content = f.read()
            # Extract Cup rows
            # <td>I</td> ... <td>Angelo Ma.</td><td>Valerio</td><td>Francesco Q.</td><td>Patrick</td>
            rows = re.findall(r'<tr>\s*<td>([IVX]+)</td>.*?<td>(.*?)</td><td>(.*?)</td><td>(.*?)</td><td>(.*?)</td>', content, re.DOTALL)
            for edition, gold, silver, bronze1, bronze2 in rows:
                for p in [gold.strip()]:
                    if p:
                        if p not in players: players[p] = []
                        players[p].append(('Oro', edition))
                for p in [silver.strip()]:
                    if p:
                        if p not in players: players[p] = []
                        players[p].append(('Argento', edition))
                for p in [bronze1.strip(), bronze2.strip()]:
                    if p:
                        if p not in players: players[p] = []
                        players[p].append(('Bronzo', edition))

    # 2. Check Cup files for other results
    for i in range(1, 14):
        roman = ["", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII"][i]
        filename = f'FNPPcup{roman}.html'
        file_path = os.path.join(directory, filename)
        if not os.path.exists(file_path): continue
        
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
            # Find all phases
            # <h2 ...>Phase Name</h2>
            # Then match-items under it
            sections = re.split(r'<h2.*?>', content)
            for section in sections[1:]:
                # Extract phase name
                phase_match = re.match(r'([^<]+)', section)
                if not phase_match: continue
                phase_name = phase_match.group(1).strip()
                
                # Check match items
                matches = re.findall(r'class="match-item".*?>(.*?) - (.*?) <span', section)
                for p1, p2 in matches:
                    p1, p2 = p1.strip(), p2.strip()
                    if p1 and p1 != 'TBD':
                        if p1 not in players: players[p1] = []
                        players[p1].append((phase_name, roman))
                    if p2 and p2 != 'TBD':
                        if p2 not in players: players[p2] = []
                        players[p2].append((phase_name, roman))

    # Filter to only the absolute best level for each player
    results = {}
    for player, occurrences in players.items():
        # Map phases to priority
        valid_occurrences = []
        for phase, edition in occurrences:
            # Normalize phase
            norm_phase = phase
            for k, v in normalization.items():
                if k in phase:
                    norm_phase = v
                    break
            
            # Find in priority map
            priority = 0
            best_label = norm_phase
            for p_key, p_val in phases_priority.items():
                if p_key in phase:
                    priority = p_val
                    best_label = p_key # Use the standard key
                    break
            if priority > 0:
                valid_occurrences.append({'phase': best_label, 'edition': edition, 'priority': priority})
        
        if not valid_occurrences: continue
        
        max_priority = max(o['priority'] for o in valid_occurrences)
        best_phases = [o for o in valid_occurrences if o['priority'] == max_priority]
        
        # Remove duplicates (same phase, same edition)
        unique_editions = []
        seen_editions = set()
        for p in best_phases:
            if p['edition'] not in seen_editions:
                unique_editions.append(p['edition'])
                seen_editions.add(p['edition'])
        
        # Sort editions Roman style
        def roman_to_int(r):
            vals = {'I':1, 'V':5, 'X':10}
            res = 0
            for i in range(len(r)):
                if i+1 < len(r) and vals[r[i]] < vals[r[i+1]]:
                    res -= vals[r[i]]
                else:
                    res += vals[r[i]]
            return res
        
        unique_editions.sort(key=roman_to_int)
        
        results[player] = {
            'phase': best_phases[0]['phase'],
            'editions': unique_editions
        }

    return results

# Run it
directory = r'c:\Users\Duran\.gemini\antigravity\scratch\fnpp'
all_results = get_best_results(directory)

# Print for comparison
for p in sorted(all_results.keys()):
    res = all_results[p]
    eds = " e ".join([f"{e} edizione" for e in res['editions']])
    if len(res['editions']) > 2:
        eds = ", ".join([f"{e} edizione" for e in res['editions'][:-1]]) + " e " + f"{res['editions'][-1]} edizione"
    
    print(f"{p}: {res['phase']} ({eds})")
