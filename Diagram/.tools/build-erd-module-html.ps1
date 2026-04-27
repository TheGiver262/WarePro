$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mdPath = Join-Path $root 'ERD module mermaid.md'
$htmlPath = Join-Path $root 'ERD module.html'
$svgDir = Join-Path $root 'plantuml-svg-erd-module'

$md = Get-Content -LiteralPath $mdPath -Raw -Encoding UTF8
$matches = [regex]::Matches($md, '(?ms)^##\s+(.*?)\r?\n\r?\n```mermaid\r?\n(.*?)\r?\n```')

function HtmlEncode([string]$text) {
  return [System.Net.WebUtility]::HtmlEncode($text)
}

$cards = New-Object System.Collections.Generic.List[string]
$index = 0
foreach ($m in $matches) {
  $index++
  $title = $m.Groups[1].Value.Trim()
  $code = $m.Groups[2].Value.Trim()
  $svgName = switch ($index) {
    1 { 'ERD_Module_01_Core_Catalog.svg' }
    2 { 'ERD_Module_02_Inventory_Flow.svg' }
    3 { 'ERD_Module_03_Invoicing.svg' }
    4 { 'ERD_Module_04_Warranty.svg' }
    5 { 'ERD_Module_05_User_Audit.svg' }
  }
  $svgPath = "plantuml-svg-erd-module/$svgName"
  $cards.Add(@"
<section class="diagram-card">
  <header>
    <span class="badge">ERD-$('{0:D2}' -f $index)</span>
    <h2>$(HtmlEncode $title)</h2>
  </header>
  <div class="diagram-wrap mermaid-pane"><pre class="mermaid">$(HtmlEncode $code)</pre></div>
  <div class="diagram-wrap plantuml-pane"><img src="$svgPath" alt="$(HtmlEncode $title) - PlantUML" /></div>
</section>
"@)
}

$body = [string]::Join("`n", $cards)
$html = @"
<!doctype html>
<html lang="vi">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>ERD module</title>
  <script type="module">
    import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.esm.min.mjs';
    mermaid.initialize({ startOnLoad: true, securityLevel: 'loose', theme: 'base', er: { useMaxWidth: false } });
  </script>
  <style>
    :root {
      --bg: #f5efe5;
      --panel: #fffaf0;
      --ink: #1d211f;
      --muted: #65645f;
      --line: #ddcbb0;
      --accent: #0f766e;
      --accent-2: #c47f2c;
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      background: radial-gradient(circle at 10% 0%, #fff7d6 0, transparent 32rem), linear-gradient(135deg, #f7efe1, #efe3d0);
      color: var(--ink);
      font-family: "Segoe UI", "Noto Sans", Arial, sans-serif;
    }
    .hero {
      padding: 36px clamp(20px, 4vw, 64px) 20px;
      border-bottom: 1px solid var(--line);
    }
    h1 { margin: 0 0 10px; font-size: clamp(34px, 5vw, 72px); letter-spacing: -0.05em; }
    .hero p { max-width: 980px; margin: 0; color: var(--muted); font-size: 18px; line-height: 1.65; }
    .toolbar {
      position: sticky;
      top: 0;
      z-index: 10;
      display: flex;
      gap: 10px;
      align-items: center;
      padding: 14px clamp(20px, 4vw, 64px);
      background: rgba(255, 250, 240, 0.92);
      backdrop-filter: blur(10px);
      border-bottom: 1px solid var(--line);
    }
    .toolbar button {
      border: 1px solid var(--line);
      border-radius: 999px;
      padding: 10px 16px;
      background: #fff;
      color: var(--ink);
      cursor: pointer;
      font-weight: 700;
    }
    .toolbar button.active { background: var(--accent); color: white; border-color: var(--accent); }
    main { padding: 22px clamp(20px, 4vw, 64px) 60px; }
    .diagram-card {
      margin: 0 0 28px;
      padding: 20px;
      background: rgba(255, 250, 240, 0.92);
      border: 1px solid var(--line);
      border-radius: 24px;
      box-shadow: 0 18px 45px rgba(72, 48, 17, 0.10);
    }
    .diagram-card header { display: flex; gap: 14px; align-items: center; margin-bottom: 14px; }
    .badge {
      display: inline-flex;
      padding: 6px 10px;
      border-radius: 999px;
      background: #e7f4f1;
      color: var(--accent);
      font-weight: 800;
      font-size: 12px;
    }
    h2 { margin: 0; font-size: clamp(22px, 2.4vw, 34px); }
    .diagram-wrap {
      overflow: auto;
      scrollbar-gutter: stable both-edges;
      overscroll-behavior: contain;
      min-height: 520px;
      padding: 18px;
      border-radius: 18px;
      background: #ffffff;
      border: 1px solid #ead9bd;
    }
    .diagram-wrap .mermaid {
      width: max-content;
      min-width: 100%;
    }
    .diagram-wrap svg { min-width: 1100px; max-width: none !important; height: auto !important; }
    .diagram-wrap img { min-width: 1100px; max-width: none; height: auto; display: block; }
    .plantuml-pane { display: none; }
    body.show-plantuml .mermaid-pane { display: none; }
    body.show-plantuml .plantuml-pane { display: block; }
    @media (max-width: 720px) {
      .diagram-wrap { min-height: 420px; }
      .diagram-wrap svg, .diagram-wrap img { min-width: 900px; }
    }
  </style>
</head>
<body>
  <section class="hero">
    <h1>ERD module</h1>
    <p>5 sơ đồ ERD module được tách từ ERD chi tiết. Có thể chuyển giữa Mermaid và PlantUML; mỗi module lặp lại các bảng cần thiết để đọc độc lập.</p>
  </section>
  <nav class="toolbar">
    <button id="btn-mermaid" class="active" type="button">Mermaid</button>
    <button id="btn-plantuml" type="button">PlantUML</button>
  </nav>
  <main>
$body
  </main>
  <script>
    function normalizeMermaidDiagramWidths() {
      document.querySelectorAll('.mermaid-pane svg').forEach((svg) => {
        const viewBox = svg.viewBox && svg.viewBox.baseVal;
        if (viewBox && viewBox.width) {
          const width = Math.ceil(viewBox.width);
          svg.style.width = width + 'px';
          svg.style.minWidth = width + 'px';
          svg.style.maxWidth = 'none';
          svg.style.height = 'auto';
        }
      });
    }

    function watchMermaidDiagramWidths() {
      const observer = new MutationObserver(() => normalizeMermaidDiagramWidths());
      document.querySelectorAll('.mermaid-pane').forEach((pane) => {
        observer.observe(pane, { childList: true, subtree: true });
      });
    }

    const mermaidBtn = document.getElementById('btn-mermaid');
    const plantBtn = document.getElementById('btn-plantuml');
    mermaidBtn.addEventListener('click', () => {
      document.body.classList.remove('show-plantuml');
      mermaidBtn.classList.add('active');
      plantBtn.classList.remove('active');
      window.setTimeout(normalizeMermaidDiagramWidths, 0);
    });
    plantBtn.addEventListener('click', () => {
      document.body.classList.add('show-plantuml');
      plantBtn.classList.add('active');
      mermaidBtn.classList.remove('active');
    });
    window.addEventListener('load', () => {
      watchMermaidDiagramWidths();
      window.setTimeout(normalizeMermaidDiagramWidths, 150);
    });
  </script>
</body>
</html>
"@

Set-Content -LiteralPath $htmlPath -Value $html -Encoding UTF8
Write-Host "Built $htmlPath from $($matches.Count) Mermaid diagrams."

