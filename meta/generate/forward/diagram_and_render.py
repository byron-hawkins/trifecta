# author: Copilot
"""diagram-and-render <source.md>: forward-compile an annotated architecture
Markdown document into its SMD, diagram files, and rendered Markdown/HTML.

usage: diagram_and_render.py <source.md> [<source.md> ...]   (run from the repository root)
"""
import html, json, os, re, sys

ROOT = os.getcwd()
SCHEMA = json.load(open(os.path.join(ROOT, 'meta/grammar/architecture-schema.json')))
MEMBER_MODS = {'public', 'internal', 'protected', 'private', 'static', 'readonly', 'const', 'abstract',
               'virtual', 'override', 'sealed', 'async', 'extern', 'required'}
PARAM_MODS = {'ref', 'out', 'in', 'params', 'this'}
ACCESS = {'public', 'internal', 'protected', 'private'}


def split_top(s, sep=','):
    out, depth, cur = [], 0, ''
    for c in s:
        if c in '<([': depth += 1
        if c in '>)]': depth -= 1
        if c == sep and depth == 0:
            out.append(cur.strip()); cur = ''
        else:
            cur += c
    if cur.strip(): out.append(cur.strip())
    return out


def type_ref(text):
    text = text.strip()
    ref = {}
    nullable = text.endswith('?')
    if nullable: text = text[:-1]
    rank = 0
    m = re.match(r'^(.*?)\[(,*)\]$', text)
    if m: text, rank = m.group(1), len(m.group(2)) + 1
    m = re.match(r'^([^<]+)<(.*)>$', text)
    if m:
        ref['name'] = m.group(1)
        ref['type_arguments'] = [type_ref(a) for a in split_top(m.group(2))]
    else:
        ref['name'] = text
    if rank: ref['array_rank'] = rank
    if nullable: ref['nullable'] = True
    return ref


def take_mods(tokens, allowed):
    mods = []
    while len(tokens) > 1 and tokens[0] in allowed:
        mods.append(tokens.pop(0))
    return sorted(mods)


def parse_params(s):
    out = []
    for p in split_top(s):
        default = None
        if ' = ' in p: p, default = p.split(' = ', 1)
        toks = p.strip().split(' ')
        mods = take_mods(toks, PARAM_MODS)
        name = toks[-1]
        param = {'name': name, 'type': type_ref(' '.join(toks[:-1]))}
        if mods: param['modifiers'] = mods
        if default is not None: param['default_value'] = default.strip()
        out.append(param)
    return out


def base_relation(name, kind, forced=None):
    if forced: return forced
    if kind == 'interface': return 'extends'
    simple = name.split('.')[-1].split('<')[0]
    return 'implements' if re.match(r'^I[A-Z]', simple) else 'extends'


def base_type(text, kind, forced=None):
    r = type_ref(text)
    b = {'name': r['name'], 'relation': base_relation(r['name'], kind, forced)}
    if 'type_arguments' in r: b['type_arguments'] = r['type_arguments']
    b['source_text'] = text
    return b


def parse_decl(text):
    m = re.match(r'^(class|interface|struct|enum) ([A-Za-z_]\w*)(?:<([^>]*)>)?(?: : (.*))?$', text)
    kind, name, tps, bases = m.groups()
    return kind, name, [t.strip() for t in tps.split(',')] if tps else [], split_top(bases) if bases else []


def parse_document(path):
    lines = open(path).read().split('\n')
    if lines and lines[0].strip().lower() == '*author: copilot*': lines = lines[1:]
    items, md, i = [], [], 0

    def flush():
        text = '\n'.join(md).strip('\n')
        if text.strip(): items.append(('markdown', text))
        md.clear()

    while i < len(lines):
        ln = lines[i]
        m = re.match(r'^<!--- @context (namespace|class) (\S+) --->$', ln)
        if m:
            flush(); items.append(('context', m.group(1), m.group(2))); i += 1; continue
        if ln == '<!--- @alias-list --->':
            flush(); i += 1; al = []
            while lines[i].strip():
                al.append(lines[i]); i += 1
            items.append(('alias-list', al)); continue
        m = re.match(r'^<!--- @diagram \[(.*?)\] \[(.*)\] -{2,3}>$', ln)
        if m:
            flush(); items.append(('diagram', m.group(1), split_top(m.group(2)))); i += 1; continue
        if ln == '<!--- @type --->':
            flush(); i += 1; outline = []
            while lines[i].strip():
                outline.append(lines[i]); i += 1
            items.append(('type', outline)); continue
        md.append(ln); i += 1
    flush()
    return items


def build(path):
    rel = os.path.relpath(path, ROOT).replace(os.sep, '/')
    items = parse_document(path)
    contexts, occ, active_ns, active_cls = [], {}, None, None
    blocks, types, alias_lists, diagrams, diags = [], [], [], [], []
    pending = []  # (outline, ns, cls, ctxs)

    for it in items:
        if it[0] == 'markdown':
            blocks.append({'kind': 'markdown', 'text': it[1]})
        elif it[0] == 'context':
            key = (it[1], it[2]); occ[key] = occ.get(key, 0) + 1
            c = {'id': f'context:{it[1]}:{it[2]}:{occ[key]}', 'kind': it[1], 'name': it[2]}
            contexts.append(c); blocks.append({'kind': 'context', 'context_id': c['id']})
            if it[1] == 'namespace': active_ns = c
            else: active_cls = c
        elif it[0] == 'alias-list':
            al = []
            for l in it[1]:
                m = re.match(r'^- global using (\w+) = (.*)$', l)
                al.append({'name': m.group(1), 'target': type_ref(m.group(2)), 'source_text': l[len('- global '):]})
            alias_lists.append({'contexts': [c for c in (active_ns, active_cls) if c], 'aliases': al})
            blocks.append({'kind': 'alias_list', 'alias_list_index': len(alias_lists) - 1})
        elif it[0] == 'type':
            ctx = [dict(kind=c['kind'], name=c['name'], id=c['id']) for c in (active_ns, active_cls) if c]
            pending.append((it[1], active_ns, active_cls, ctx))
            blocks.append({'kind': 'type', 'type_id': None, 'idx': len(pending) - 1})
        elif it[0] == 'diagram':
            diagrams.append({'path': it[1], 'types': it[2]})
            blocks.append({'kind': 'diagram', 'diagram_index': len(diagrams) - 1})
        # a heading ends the class context when unannotated
    # ends of class context at unannotated level-two headings
    types, ids_by_name = [], {}
    active_cls = None
    # second pass to apply class-context end rule accurately
    seq = []
    cls = None
    for idx, it in enumerate(items):
        if it[0] == 'context' and it[1] == 'class': cls = it[2]
        elif it[0] == 'markdown':
            prev = items[idx - 1] if idx else None
            if cls and re.search(r'^## ', it[1], re.M):
                first = it[1].lstrip('\n').split('\n')[0]
                if not (prev and prev[0] == 'context' and prev[1] == 'class' and first.startswith('## ')):
                    cls = None
        elif it[0] == 'type': seq.append(cls)
    for (outline, ns, _, ctx), cls_name in zip(pending, seq):
        ctx = [c for c in ctx if c['kind'] == 'namespace' or c['name'] == cls_name]
        if cls_name is None: ctx = [c for c in ctx if c['kind'] == 'namespace']
        types.append(build_type(outline, ns['name'] if ns else None, cls_name, ctx, types, rel, diags))

    for b in blocks:
        if b['kind'] == 'type':
            b['type_id'] = types[b.pop('idx')]['id']
    blocks = [{k: v for k, v in b.items()} for b in blocks]

    by = {}
    for t in types: by.setdefault(t['name'], []).append(t)
    for d in diagrams:
        resolved = []
        for spec in d['types']:
            m = re.match(r'^(class|interface|struct|enum) (\w+)(?:<(.*)>)?$', spec)
            kind, name, tps = m.groups(); tps = [x.strip() for x in tps.split(',')] if tps else []
            t = next((x for x in by.get(name, []) if x['kind'] == kind and x.get('type_parameters', []) == tps), None)
            if not t:
                diags.append({'severity': 'error', 'code': 'diagram-type-unresolved',
                              'message': f'Diagram {d["path"]} selects {spec}, which has no type annotation.'})
                continue
            e = {'kind': kind, 'name': name}
            if tps: e['type_parameters'] = tps
            e['type_id'] = t['id']
            resolved.append(e)
        d['types'] = resolved

    smd = {'author': 'Copilot', 'format': 'markdown-architecture-smd', 'version': 1,
           'source': {'kind': 'markdown', 'path': rel}, 'contexts': contexts, 'blocks': blocks, 'types': types}
    if alias_lists: smd['global_alias_lists'] = alias_lists
    smd['diagrams'] = diagrams
    smd['diagnostics'] = diags
    return smd, items


def build_type(outline, ns, cls_name, ctx, prior, rel, diags):
    kind, name, tps, bases = parse_decl(outline[0][2:])
    t = {'id': None, 'kind': kind, 'name': name}
    if tps: t['type_parameters'] = tps
    t['namespace'] = ns
    arity = f'`{len(tps)}' if tps else ''
    container = None
    if cls_name and cls_name != name:
        container = next((p for p in prior if p['name'] == cls_name), None)
    if container:
        t['id'] = container['id'] + '+' + name + arity
        t['containing_type_id'] = container['id']
    else:
        t['id'] = f'type:{ns}.{name}{arity}' if ns else f'type:{name}{arity}'
    t['contexts'] = ctx
    t['modifiers'] = []
    base_types = [base_type(b, kind) for b in bases]
    aliases, members = [], []
    cat = None
    for ln in outline[1:]:
        if ln.startswith('  - '): cat = ln[4:].strip(); continue
        e = ln[6:].strip()
        if cat == 'inherits': base_types.append(base_type(e, kind))
        elif cat == 'capabilities': base_types.append(base_type(e, kind, 'implements'))
        elif cat == 'types':
            m = re.match(r'^using (\w+) = (.*)$', e)
            if m: aliases.append({'name': m.group(1), 'target': type_ref(m.group(2)), 'source_text': e})
        elif cat == 'composition':
            if e == '[singleton]':
                members.append({'kind': 'field', 'name': 'singleton', 'type': {'name': name}, 'modifiers': ['public', 'static'], 'annotations': ['singleton']})
            elif kind == 'enum':
                members.append({'kind': 'enum_member', 'name': e})
            else:
                toks = e.split(' ') if '<' not in e else None
                m = re.match(r'^(.*\S)\s+(\w+)$', e)
                head, fname = m.group(1), m.group(2)
                ht = re.split(r' (?![^<]*>)', head)
                mods = take_mods(ht, MEMBER_MODS)
                if not (set(mods) & ACCESS): mods = sorted(mods + ['private'])
                members.append({'kind': 'field', 'name': fname, 'type': type_ref(' '.join(ht)), 'modifiers': mods})
        elif cat == 'operations':
            m = re.match(r'^(.*?)(?:<([^>(]*)>)?\((.*)\)$', e)
            head, mtps, params = m.groups()
            ht = head.split(' ')
            mods = take_mods(ht, MEMBER_MODS)
            if len(ht) == 1 and ht[0] == name:
                members.append({'kind': 'constructor', 'name': name, 'modifiers': mods, 'parameters': parse_params(params), 'source_text': e})
            else:
                mem = {'kind': 'method', 'name': ht[-1], 'return_type': type_ref(' '.join(ht[:-1]))}
                if mtps: mem['type_parameters'] = [x.strip() for x in mtps.split(',')]
                mem.update({'modifiers': mods, 'parameters': parse_params(params), 'source_text': e})
                members.append(mem)
    t['base_types'] = base_types
    t['aliases'] = aliases
    t['members'] = members
    return t


def reorder(v, node):
    """Order object keys following the schema's declared property order."""
    if isinstance(node, dict) and '$ref' in node:
        node = SCHEMA['$defs'][node['$ref'].split('/')[-1]]
    if isinstance(v, list):
        item = node.get('items') if isinstance(node, dict) else None
        return [reorder(x, item) for x in v]
    if isinstance(v, dict):
        if isinstance(node, dict) and 'oneOf' in node:
            kinds = {x.get('properties', {}).get('kind', {}).get('const'): x for x in
                     (SCHEMA['$defs'][o['$ref'].split('/')[-1]] if '$ref' in o else o for o in node['oneOf'])}
            node = kinds.get(v.get('kind'), {})
        props = node.get('properties', {}) if isinstance(node, dict) else {}
        out = {k: reorder(v[k], props.get(k)) for k in props if k in v}
        for k in sorted(k for k in v if k not in props): out[k] = reorder(v[k], None)
        return out
    return v


# ---------- Mermaid ----------
def mm(ref):
    s = ref['name']
    if 'type_arguments' in ref: s += '~' + ', '.join(mm(a) for a in ref['type_arguments']) + '~'
    if 'array_rank' in ref: s += '[' + ',' * (ref['array_rank'] - 1) + ']'
    if ref.get('nullable'): s += '?'
    return s


def refnames(ref):
    yield ref['name']
    for a in ref.get('type_arguments', []): yield from refnames(a)


def mermaid(smd, d):
    by_id = {t['id']: t for t in smd['types']}
    sel = [by_id[e['type_id']] for e in d['types']]
    names = {t['name']: t for t in sel}
    out = ['classDiagram']
    spaces = []
    for t in sel:
        if t['namespace'] not in spaces: spaces.append(t['namespace'])
    for ns in spaces:
        out.append(f'namespace {ns} {{')
        for t in [x for x in sel if x['namespace'] == ns]:
            cname = t['name'] + ('~' + ', '.join(t['type_parameters']) + '~' if t.get('type_parameters') else '')
            out.append(f'  class {cname} {{')
            st = {'interface': 'interface', 'enum': 'enumeration', 'struct': 'struct'}.get(t['kind'])
            if st: out.append(f'    <<{st}>>')
            for m in t['members']:
                if m['kind'] == 'enum_member': out.append(f'    {m["name"]}')
                elif m['kind'] == 'field':
                    if 'singleton' in m.get('annotations', []): out.append('    singleton')
                    else:
                        vis = '+' if 'public' in m['modifiers'] else ''
                        out.append(f'    {vis}{mm(m["type"])} {m["name"]}')
                else:
                    ps = ', '.join(f'{mm(p["type"])} {p["name"]}' for p in m['parameters'])
                    ret = ' ' + mm(m['return_type']) if m['kind'] == 'method' else ''
                    out.append(f'    +{m["name"]}({ps}){ret}')
            out.append('  }')
        out.append('}')
    for t in sel:
        label = lambda x: x['name'] + ('~' + ', '.join(x['type_parameters']) + '~' if x.get('type_parameters') else '')
        for m in t['members']:
            if m['kind'] == 'field' and 'singleton' not in m.get('annotations', []):
                for n in dict.fromkeys(refnames(m['type'])):
                    if n in names and names[n] is not t:
                        out.append(f'{label(t)} *-- {label(names[n])} : {m["name"]}')
        for b in t['base_types']:
            if b['name'] in names:
                arrow = '..|>' if b['relation'] == 'implements' or t['kind'] == 'interface' else '--|>'
                out.append(f'{label(t)} {arrow} {label(names[b["name"]])}')
    return '\n'.join(out)


def to_html(md_text, title):
    # minimal renderer for the constructs used by these documents
    lines, out, i = md_text.split('\n'), [], 0
    def inline(s):
        s = html.escape(s, quote=False)
        s = re.sub(r'`([^`]+)`', r'<code>\1</code>', s)
        return re.sub(r'\*([^*]+)\*', r'<em>\1</em>', s)
    def ul(block):
        res, depth = [], 0
        stack = []
        for ln in block:
            lvl = (len(ln) - len(ln.lstrip(' '))) // 2
            while len(stack) <= lvl:
                res.append('  ' * (len(stack) + 1) + '<ul>'); stack.append(0)
            while len(stack) > lvl + 1:
                res.append('  ' * len(stack) + '</ul>'); stack.pop()
            res.append('  ' * (lvl + 2) + f'<li>{inline(ln.strip()[2:])}</li>')
        while stack:
            res.append('  ' * len(stack) + '</ul>'); stack.pop()
        return res
    while i < len(lines):
        ln = lines[i]
        if ln.startswith('```mermaid'):
            j = i + 1; body = []
            while lines[j] != '```': body.append(lines[j]); j += 1
            out.append('  <pre class="mermaid">\n' + html.escape('\n'.join(body), quote=False) + '\n  </pre>')
            out.append('  <p><small>Diagrams use Mermaid from jsDelivr; rendering requires an internet connection.</small></p>')
            i = j + 1; continue
        m = re.match(r'^(#{1,6}) (.*)$', ln)
        if m: out.append(f'  <h{len(m.group(1))}>{inline(m.group(2))}</h{len(m.group(1))}>'); i += 1; continue
        if ln.startswith('- '):
            block = []
            while i < len(lines) and lines[i].strip() and lines[i].lstrip().startswith('- '):
                block.append(lines[i]); i += 1
            out.extend(ul(block)); continue
        if ln.startswith('<!---'): i += 1; continue
        if ln.strip():
            para = []
            while i < len(lines) and lines[i].strip() and not lines[i].startswith(('#', '- ', '<!---', '```')):
                para.append(lines[i]); i += 1
            out.append('  <p>' + inline(' '.join(para)) + '</p>'); continue
        i += 1
    return '\n'.join(out)


HTML = '''<!-- author: Copilot -->
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{title}</title>
  <style>
    :root {{ color-scheme: light dark; font-family: system-ui, sans-serif; }}
    body {{ line-height: 1.55; margin: 0 auto; max-width: 72rem; padding: 2rem; }}
    .mermaid {{ overflow-x: auto; }}
    code {{ font-family: ui-monospace, monospace; }}
  </style>
  <script src="https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js"></script>
  <script>
    document.addEventListener("DOMContentLoaded", () => {{
      mermaid.initialize({{ startOnLoad: true, securityLevel: "strict" }});
    }});
  </script>
</head>
<body>
{body}
</body>
</html>
'''


def main(path):
    path = os.path.abspath(path)
    smd, items = build(path)
    if not smd['types']:
        print(f'{path}: no types; skipped'); return
    base = os.path.splitext(os.path.basename(path))[0]
    docdir = os.path.dirname(path)
    os.makedirs(os.path.join(docdir, 'bin'), exist_ok=True)
    smd = reorder(smd, SCHEMA)
    with open(os.path.join(docdir, 'bin', base + '.smd'), 'w', newline='\n') as f:
        f.write(json.dumps(smd, indent=2, ensure_ascii=False) + '\n')
    figs = {}
    for d in smd['diagrams']:
        figs[d['path']] = mermaid(smd, d)
        fp = os.path.join(docdir, d['path']); os.makedirs(os.path.dirname(fp), exist_ok=True)
        with open(fp, 'w', newline='\n') as f:
            f.write('*author: Copilot*\n\n```mermaid\n' + figs[d['path']] + '\n```\n')
    src_lines = open(path).read().split('\n')
    if src_lines[0].strip().lower() == '*author: copilot*': src_lines = src_lines[1:]
    seen = {}
    for d in smd['diagrams']:
        name = os.path.basename(d['path'])
        if name in seen: raise SystemExit(f'render collision on {name}')
        seen[name] = 1
        text = '\n'.join(src_lines)
        text = re.sub(r'^<!--- @diagram \[(.*?)\] \[.*\] -{2,3}>$',
                      lambda m: '```mermaid\n' + figs[m.group(1)] + '\n```', text, flags=re.M)
        text = '*author: Copilot*\n\n' + text.lstrip('\n')
        os.makedirs(os.path.join(docdir, 'render'), exist_ok=True)
        stem = os.path.splitext(name)[0]
        with open(os.path.join(docdir, 'render', stem + '.md'), 'w', newline='\n') as f: f.write(text)
        title = re.search(r'^Subject: .*$', text, re.M)
        with open(os.path.join(docdir, 'render', stem + '.html'), 'w', newline='\n') as f:
            f.write(HTML.format(title=f'{base.capitalize()} architecture', body=to_html(text, base)))
    print(f'{path}: {len(smd["types"])} types, {len(smd["diagrams"])} diagrams')


if __name__ == '__main__':
    for p in sys.argv[1:]: main(p)
