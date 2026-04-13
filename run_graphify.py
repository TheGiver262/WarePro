import json
from graphify.detect import detect
from graphify.extract import collect_files, extract
from pathlib import Path

# Step 1: Re-detect with .graphifyignore
result = detect(Path('.'))
Path('.graphify_detect.json').write_text(json.dumps(result, indent=2))
print('Corpus: {} files, ~{} words'.format(result['total_files'], result['total_words']))
for ftype, flist in result['files'].items():
    if flist:
        print('  {}: {} files'.format(ftype, len(flist)))

# Step 2: AST extraction
code_files = []
for f in result.get('files', {}).get('code', []):
    p = Path(f)
    code_files.extend(collect_files(p) if p.is_dir() else [p])

if code_files:
    ast_result = extract(code_files)
    Path('.graphify_ast.json').write_text(json.dumps(ast_result, indent=2))
    if isinstance(ast_result, dict):
        print('AST: {} nodes, {} edges'.format(len(ast_result.get('nodes', [])), len(ast_result.get('edges', []))))
    else:
        print('Result is not a dictionary.')
else:
    Path('.graphify_ast.json').write_text(json.dumps({'nodes':[],'edges':[],'input_tokens':0,'output_tokens':0}))
    print('No code files - skipping AST extraction')

# Step 3: Skip semantic (code-only fast path) - write empty semantic
Path('.graphify_semantic.json').write_text(json.dumps({'nodes':[],'edges':[],'hyperedges':[],'input_tokens':0,'output_tokens':0}))

# Step 4: Merge AST + semantic
ast = json.loads(Path('.graphify_ast.json').read_text())
sem = json.loads(Path('.graphify_semantic.json').read_text())

seen = {n['id'] for n in ast.get('nodes', [])}
merged_nodes = list(ast.get('nodes', []))
for n in sem.get('nodes', []):
    if n['id'] not in seen:
        merged_nodes.append(n)
        seen.add(n['id'])

merged = {
    'nodes': merged_nodes,
    'edges': ast.get('edges', []) + sem.get('edges', []),
    'hyperedges': sem.get('hyperedges', []),
    'input_tokens': sem.get('input_tokens', 0),
    'output_tokens': sem.get('output_tokens', 0),
}
Path('.graphify_extract.json').write_text(json.dumps(merged, indent=2))
print('Merged: {} nodes, {} edges'.format(len(merged_nodes), len(merged['edges'])))

# Step 5: Build graph, cluster, analyze
from graphify.build import build_from_json
from graphify.cluster import cluster, score_all
from graphify.analyze import god_nodes, surprising_connections, suggest_questions
from graphify.report import generate
from graphify.export import to_json
import os

os.makedirs('graphify-out', exist_ok=True)

G = build_from_json(merged)
communities = cluster(G)
cohesion = score_all(G, communities)
tokens = {'input': merged.get('input_tokens', 0), 'output': merged.get('output_tokens', 0)}
gods = god_nodes(G)
surprises = surprising_connections(G, communities)
labels = {cid: 'Community ' + str(cid) for cid in communities}
questions = suggest_questions(G, communities, labels)

detection = result
report = generate(G, communities, cohesion, labels, gods, surprises, detection, tokens, '.', suggested_questions=questions)
Path('graphify-out/GRAPH_REPORT.md').write_text(report)
to_json(G, communities, 'graphify-out/graph.json')

analysis = {
    'communities': {str(k): v for k, v in communities.items()},
    'cohesion': {str(k): v for k, v in cohesion.items()},
    'gods': gods,
    'surprises': surprises,
    'questions': questions,
}
Path('.graphify_analysis.json').write_text(json.dumps(analysis, indent=2))
print('Graph: {} nodes, {} edges, {} communities'.format(G.number_of_nodes(), G.number_of_edges(), len(communities)))

# Step 6: Generate HTML
from graphify.export import to_html
if G.number_of_nodes() > 5000:
    print('Graph too large for HTML viz.')
else:
    to_html(G, communities, 'graphify-out/graph.html', community_labels=labels or None)
    print('graph.html written')

print('Done! Outputs in graphify-out/')
