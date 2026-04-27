$ErrorActionPreference = 'Stop'

Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $root 'warehouse_management_vi_senior_reviewed_mermaid.md'
$outputPath = Join-Path $root 'warehouse_diagrams.html'

function HtmlEncode([string]$text) {
    return [System.Net.WebUtility]::HtmlEncode($text)
}

function Convert-MarkdownTextToHtml([string]$text) {
    if ([string]::IsNullOrWhiteSpace($text)) { return '' }

    $trimmed = $text.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) { return '' }

    $lines = $trimmed -split "`r?`n"
    $items = [System.Collections.Generic.List[string]]::new()
    $paras = [System.Collections.Generic.List[string]]::new()
    $buffer = [System.Collections.Generic.List[string]]::new()

    foreach ($line in $lines) {
        $t = $line.Trim()
        if ($t -match '^-\s+(.*)$') {
            if ($buffer.Count -gt 0) {
                $paras.Add(($buffer -join ' ').Trim())
                $buffer.Clear()
            }
            $items.Add($matches[1].Trim())
        } elseif ($t -eq '') {
            if ($buffer.Count -gt 0) {
                $paras.Add(($buffer -join ' ').Trim())
                $buffer.Clear()
            }
        } else {
            if ($items.Count -gt 0) {
                $paras.Add('<ul class="flat">' + (($items | ForEach-Object { '<li>' + (HtmlEncode $_) + '</li>' }) -join '') + '</ul>')
                $items.Clear()
            }
            $buffer.Add($t)
        }
    }

    if ($buffer.Count -gt 0) {
        $paras.Add(($buffer -join ' ').Trim())
    }
    if ($items.Count -gt 0) {
        $paras.Add('<ul class="flat">' + (($items | ForEach-Object { '<li>' + (HtmlEncode $_) + '</li>' }) -join '') + '</ul>')
    }

    $htmlParts = foreach ($entry in $paras) {
        if ($entry.StartsWith('<ul')) { $entry } else { '<p>' + (HtmlEncode $entry) + '</p>' }
    }

    return ($htmlParts -join "`n")
}

$raw = Get-Content -Raw -Encoding UTF8 $sourcePath
$sectionRegex = [regex]'(?ms)^##\s+(?<title>.+?)\r?\n(?<body>.*?)(?=^##\s+|\z)'
$matches = $sectionRegex.Matches($raw)

$sections = foreach ($match in $matches) {
    $body = $match.Groups['body'].Value
    $codeMatch = [regex]::Match($body, '(?ms)```mermaid\r?\n(?<code>.*?)```')
    if (-not $codeMatch.Success) { continue }

    $title = $match.Groups['title'].Value.Trim()
    $intro = $body.Substring(0, $codeMatch.Index).Trim()
    $after = $body.Substring($codeMatch.Index + $codeMatch.Length).Trim()

    [pscustomobject]@{
        Title = $title
        Mermaid = $codeMatch.Groups['code'].Value.TrimEnd()
        Intro = $intro
        Tail = $after
    }
}

$sectionMeta = @(
    @{ Id = 'ARCH-01'; Svg = 'Architecture_MVVM_WPF_SQLServer.svg'; Category = 'arch'; CardClass = 'full arch-wide'; Badge = 'arch' }
    @{ Id = 'UC-01'; Svg = 'UseCase_TongThe.svg'; Category = 'usecase'; CardClass = 'full flow-xwide'; Badge = 'use' }
    @{ Id = 'UC-02'; Svg = 'UseCase_TraCuu_BaoCao.svg'; Category = 'usecase'; CardClass = 'flow-wide'; Badge = 'use' }
    @{ Id = 'UC-03'; Svg = 'UseCase_NhapKho_HoaDonMua.svg'; Category = 'usecase'; CardClass = 'flow-wide'; Badge = 'use' }
    @{ Id = 'UC-04'; Svg = 'UseCase_XuatKho_HoaDonBan.svg'; Category = 'usecase'; CardClass = 'flow-wide'; Badge = 'use' }
    @{ Id = 'UC-05'; Svg = 'UseCase_KiemKe_DieuChinh.svg'; Category = 'usecase'; CardClass = 'flow-wide'; Badge = 'use' }
    @{ Id = 'UC-06'; Svg = 'UseCase_BaoHanh.svg'; Category = 'usecase'; CardClass = 'flow-wide'; Badge = 'use' }
    @{ Id = 'AC-01'; Svg = 'Activity_NhapKho_GhiSo.svg'; Category = 'activity'; CardClass = 'flow-wide'; Badge = 'act' }
    @{ Id = 'AC-01A'; Svg = 'Activity_ImportTonDauKy_ExcelCsv.svg'; Category = 'activity'; CardClass = 'flow-wide'; Badge = 'act' }
    @{ Id = 'AC-02'; Svg = 'Activity_XuatKho_GhiSo.svg'; Category = 'activity'; CardClass = 'flow-wide'; Badge = 'act' }
    @{ Id = 'AC-03'; Svg = 'Activity_KiemKe_DieuChinh.svg'; Category = 'activity'; CardClass = 'flow-wide'; Badge = 'act' }
    @{ Id = 'AC-04'; Svg = 'Activity_BaoHanh_DoiMoi.svg'; Category = 'activity'; CardClass = 'full flow-xwide'; Badge = 'act' }
    @{ Id = 'SEQ-00'; Svg = 'Sequence_DangNhap.svg'; Category = 'sequence'; CardClass = 'sequence-wide'; Badge = 'seq' }
    @{ Id = 'SEQ-01'; Svg = 'Sequence_NhapKho_GhiSo.svg'; Category = 'sequence'; CardClass = 'sequence-wide'; Badge = 'seq' }
    @{ Id = 'SEQ-01A'; Svg = 'Sequence_ImportTonDauKy_ExcelCsv.svg'; Category = 'sequence'; CardClass = 'sequence-wide'; Badge = 'seq' }
    @{ Id = 'SEQ-02'; Svg = 'Sequence_XuatKho_GhiSo.svg'; Category = 'sequence'; CardClass = 'sequence-wide'; Badge = 'seq' }
    @{ Id = 'SEQ-03'; Svg = 'Sequence_BaoHanh_DoiMoi.svg'; Category = 'sequence'; CardClass = 'full sequence-xwide'; Badge = 'seq' }
    @{ Id = 'STATE-01'; Svg = 'State_VongDoi_ChungTuKho.svg'; Category = 'state'; CardClass = 'state-wide'; Badge = 'state' }
    @{ Id = 'STATE-02'; Svg = 'State_VongDoi_HoSoBaoHanh.svg'; Category = 'state'; CardClass = 'state-wide'; Badge = 'state' }
    @{ Id = 'ERD-01'; Svg = 'ERD_QuanLyHangHoaBaoHanh_ChiTiet.svg'; Category = 'erd'; CardClass = 'full erd-wide'; Badge = 'erd' }
)

if ($sections.Count -ne $sectionMeta.Count) {
    throw "Section count mismatch. Mermaid source has $($sections.Count) sections, metadata has $($sectionMeta.Count)."
}

$sectionsWithMeta = for ($i = 0; $i -lt $sections.Count; $i++) {
    $section = $sections[$i]
    $meta = $sectionMeta[$i]
    [pscustomobject]@{
        Title = $section.Title
        Mermaid = $section.Mermaid
        Intro = $section.Intro
        Tail = $section.Tail
        Id = $meta.Id
        Svg = $meta.Svg
        Category = $meta.Category
        CardClass = $meta.CardClass
        Badge = $meta.Badge
    }
}

$navLinks = @(
    @{ Id = 'decisions'; Label = 'Quyết định' }
    @{ Id = 'arch'; Label = 'Kiến trúc' }
    @{ Id = 'usecase'; Label = 'Use Case' }
    @{ Id = 'activity'; Label = 'Activity' }
    @{ Id = 'sequence'; Label = 'Sequence' }
    @{ Id = 'state'; Label = 'State' }
    @{ Id = 'erd'; Label = 'ERD' }
)

$decisionsHtml = @'
    <div id="decisions" class="section-title">Quyết định Đã Khóa</div>
    <section class="grid">
      <article class="card">
        <header>
          <div class="badge-row"><span class="badge note">DECISION-01</span></div>
          <h2>Kho mở rộng nhưng UI vẫn gọn</h2>
          <p>Thiết kế đã đưa <code>Warehouse</code> trở lại schema để chuẩn bị cho nhiều kho về sau, nhưng phase hiện tại vẫn chỉ vận hành trên một kho mặc định và không lộ màn hình chọn kho cho người dùng.</p>
        </header>
        <div class="content">
          <ul class="flat">
            <li><code>StockBalance</code> được quản lý theo <code>Product + Warehouse</code>.</li>
            <li><code>StockLedger</code> mang ngữ cảnh <code>WarehouseId</code>.</li>
            <li>Application Service phải tự gán kho mặc định cho nhập, xuất, kiểm kê, điều chỉnh và import đầu kỳ.</li>
          </ul>
        </div>
      </article>

      <article class="card">
        <header>
          <div class="badge-row"><span class="badge note">DECISION-02</span></div>
          <h2>Import tồn đầu kỳ từ Excel/CSV</h2>
          <p>Import được thiết kế như workflow ở tầng ứng dụng, không thêm bảng import riêng trong phase này.</p>
        </header>
        <div class="content">
          <ul class="flat">
            <li>Dữ liệu hợp lệ sẽ sinh <code>StockIn</code> loại <code>OpeningBalance</code>.</li>
            <li>Hỗ trợ cả hàng không serial và hàng có serial cụ thể ngay từ file import.</li>
            <li>Giá vốn lấy chung theo dòng sản phẩm, không theo từng serial riêng lẻ.</li>
          </ul>
        </div>
      </article>

      <article class="card">
        <header>
          <div class="badge-row"><span class="badge note">DECISION-03</span></div>
          <h2>Thuế cơ bản ở mức thương mại</h2>
          <p>Thuế chỉ phục vụ tính tiền và in hóa đơn, không mở rộng sang kế toán thuế phức tạp.</p>
        </header>
        <div class="content">
          <ul class="flat">
            <li>Header hóa đơn lưu <code>SubTotal</code>, <code>TaxAmount</code>, <code>GrandTotal</code>.</li>
            <li>Invoice line lưu <code>TaxRate</code>, <code>TaxAmount</code>, <code>GrandTotal</code>.</li>
            <li>Không xử lý giá đã gồm thuế, nhiều lớp thuế hay bút toán thuế đầu vào/đầu ra.</li>
          </ul>
        </div>
      </article>

      <article class="card">
        <header>
          <div class="badge-row"><span class="badge note">DECISION-04</span></div>
          <h2>Bảo hành và kho tiếp tục tách rạch ròi</h2>
          <p>Luồng đổi mới bảo hành vẫn đi qua <code>StockOut WarrantyReplacement</code> làm source document chuẩn và tiếp tục giữ các rule locking, audit, coverage và claim đã chốt.</p>
        </header>
        <div class="content">
          <ul class="flat">
            <li><code>WarrantyReplacement</code> dùng phiếu xuất riêng, không trộn với xuất bán thông thường.</li>
            <li><code>ReplacementStockOutId</code> tiếp tục được lưu trực tiếp trong <code>WarrantyClaim</code>.</li>
            <li>Lock order theo <code>ProductId</code> rồi <code>ProductSerialId</code> vẫn được giữ nhất quán ở các sequence và activity.</li>
          </ul>
        </div>
      </article>
    </section>
'@

function Get-SectionWrapper([string]$category) {
    switch ($category) {
        'arch' { return @{ Id = 'arch'; Title = 'Kiến trúc'; Wrapper = 'diagram-grid' } }
        'usecase' { return @{ Id = 'usecase'; Title = 'Use Case'; Wrapper = 'stack-grid' } }
        'activity' { return @{ Id = 'activity'; Title = 'Activity'; Wrapper = 'stack-grid' } }
        'sequence' { return @{ Id = 'sequence'; Title = 'Sequence'; Wrapper = 'stack-grid' } }
        'state' { return @{ Id = 'state'; Title = 'State'; Wrapper = 'split-grid' } }
        'erd' { return @{ Id = 'erd'; Title = 'ERD'; Wrapper = 'diagram-grid' } }
        default { throw "Unknown category: $category" }
    }
}

$categoryOrder = @('arch','usecase','activity','sequence','state','erd')
$sectionsHtml = [System.Collections.Generic.List[string]]::new()

foreach ($category in $categoryOrder) {
    $group = @($sectionsWithMeta | Where-Object Category -eq $category)
    if ($group.Count -eq 0) { continue }
    $wrapper = Get-SectionWrapper $category
    $sectionsHtml.Add("    <div id=""$($wrapper.Id)"" class=""section-title"">$($wrapper.Title)</div>")
    $sectionsHtml.Add("    <section class=""$($wrapper.Wrapper)"">")

    foreach ($section in $group) {
        $titleNoPrefix = ($section.Title -replace '^\S+\.\s*', '').Trim()
        $introHtml = Convert-MarkdownTextToHtml $section.Intro
        $tailHtml = Convert-MarkdownTextToHtml $section.Tail
        $svgSrc = "plantuml-svg/$($section.Svg)"
        $encodedMermaid = HtmlEncode($section.Mermaid)

        $sectionsHtml.Add(@"
      <article class="card $($section.CardClass)">
        <header>
          <div class="badge-row"><span class="badge $($section.Badge)">$($section.Id)</span></div>
          <h2>$(HtmlEncode $titleNoPrefix)</h2>
          $(if ($introHtml) { $introHtml } else { '<p>Nội dung của card này được dựng trực tiếp từ source Mermaid chuẩn để tránh lệch giữa tài liệu và bản render.</p>' })
        </header>
        $(if ($section.Category -eq 'erd') { '<div class="scroll-slider-wrap" data-scroll-slider-wrap><span>Kéo ngang ERD</span><input type="range" class="scroll-slider" data-scroll-slider min="0" max="0" value="0" step="1"></div>' } else { '' })
        <div class="diagram mermaid" data-engine-pane="mermaid">
$encodedMermaid
        </div>
        <div class="diagram plantuml-pane" data-engine-pane="plantuml">
          <img src="$(HtmlEncode $svgSrc)" alt="$(HtmlEncode $titleNoPrefix)">
        </div>
        $(if ($tailHtml) { '<div class="content">' + $tailHtml + '</div>' } else { '' })
      </article>
"@)
    }

    $sectionsHtml.Add('    </section>')
}

$navHtml = ($navLinks | ForEach-Object { '<a href="#' + $_.Id + '">' + (HtmlEncode $_.Label) + '</a>' }) -join "`n    "
$bodySections = ($sectionsHtml -join "`n")

$html = @"
<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Diagram quản lý hàng hóa và bảo hành</title>
  <script src="https://cdnjs.cloudflare.com/ajax/libs/mermaid/10.9.0/mermaid.min.js"></script>
  <style>
    :root {
      --bg: #f5f7fb;
      --surface: #ffffff;
      --surface-soft: #f8fbff;
      --border: #d7e1ec;
      --text: #19324c;
      --muted: #62758a;
      --blue: #2b7cd3;
      --green: #2d9364;
      --amber: #be7b21;
      --purple: #7a4db4;
      --shadow: 0 16px 32px rgba(23, 50, 77, 0.06);
    }

    * { box-sizing: border-box; }

    body {
      margin: 0;
      font-family: "Segoe UI", "Noto Sans", sans-serif;
      color: var(--text);
      background:
        radial-gradient(circle at top right, rgba(43,124,211,0.08), transparent 24%),
        radial-gradient(circle at left top, rgba(45,147,100,0.08), transparent 20%),
        var(--bg);
      line-height: 1.58;
    }

    header.hero {
      padding: 44px 24px 28px;
      border-bottom: 1px solid var(--border);
      background: linear-gradient(135deg, #ffffff, #f0f6fd);
    }

    .hero h1 {
      margin: 0 0 10px;
      font-size: 2.15rem;
      font-weight: 800;
    }

    .hero p {
      max-width: 1120px;
      margin: 0;
      color: var(--muted);
    }

    nav {
      position: sticky;
      top: 0;
      z-index: 10;
      display: flex;
      gap: 8px;
      padding: 14px 24px;
      overflow-x: auto;
      background: rgba(255,255,255,0.95);
      border-bottom: 1px solid var(--border);
      backdrop-filter: blur(8px);
    }

    nav a {
      text-decoration: none;
      color: var(--muted);
      padding: 8px 12px;
      border-radius: 999px;
      border: 1px solid var(--border);
      white-space: nowrap;
      font-size: 0.92rem;
    }

    nav a:hover {
      color: var(--text);
      border-color: var(--blue);
    }

    .engine-bar {
      position: sticky;
      top: 58px;
      z-index: 9;
      border-bottom: 1px solid var(--border);
      background: rgba(255,255,255,0.96);
      backdrop-filter: blur(8px);
    }

    .engine-bar-inner {
      max-width: 1880px;
      margin: 0 auto;
      padding: 14px 24px;
      display: flex;
      gap: 16px;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
    }

    .engine-copy strong {
      display: block;
      margin-bottom: 4px;
    }

    .engine-copy span {
      color: var(--muted);
      font-size: 0.93rem;
    }

    .engine-toggle {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
    }

    .engine-btn {
      border: 1px solid var(--border);
      background: #fff;
      border-radius: 999px;
      padding: 8px 12px;
      cursor: pointer;
      color: var(--muted);
      font-size: 0.92rem;
    }

    .engine-btn:hover,
    .engine-btn.is-active {
      color: var(--text);
      border-color: var(--blue);
    }

    main {
      max-width: 1880px;
      margin: 0 auto;
      padding: 32px 24px 96px;
    }

    .section-title {
      margin: 36px 4px 16px;
      color: var(--muted);
      font-size: 0.82rem;
      font-weight: 700;
      letter-spacing: 1.8px;
      text-transform: uppercase;
    }

    .grid {
      display: grid;
      gap: 20px;
      grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
    }

    .diagram-grid,
    .stack-grid,
    .split-grid {
      display: grid;
      gap: 20px;
    }

    .diagram-grid { grid-template-columns: repeat(auto-fit, minmax(460px, 1fr)); }
    .stack-grid { grid-template-columns: 1fr; }
    .split-grid { grid-template-columns: repeat(auto-fit, minmax(560px, 1fr)); }

    .full { grid-column: 1 / -1; }

    .card {
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 18px;
      box-shadow: var(--shadow);
      overflow: hidden;
    }

    .card > header {
      padding: 16px 18px 12px;
      border-bottom: 1px solid var(--border);
      background: var(--surface-soft);
    }

    .card h2 {
      margin: 0;
      font-size: 1.05rem;
    }

    .card p {
      margin: 8px 0 0;
      color: var(--muted);
      font-size: 0.94rem;
    }

    .card .content {
      padding: 18px;
    }

    .scroll-slider-wrap {
      display: none;
      align-items: center;
      gap: 12px;
      padding: 10px 16px;
      border-bottom: 1px solid var(--border);
      background: linear-gradient(180deg, #f8fbff, #eef5fc);
    }

    .scroll-slider-wrap span {
      color: var(--muted);
      font-size: 0.9rem;
      white-space: nowrap;
    }

    .scroll-slider {
      flex: 1 1 auto;
      width: 100%;
    }

    .badge-row {
      display: flex;
      align-items: center;
      gap: 8px;
      margin-bottom: 8px;
      flex-wrap: wrap;
    }

    .badge {
      display: inline-block;
      padding: 4px 8px;
      border-radius: 8px;
      font-size: 0.73rem;
      font-weight: 700;
      letter-spacing: 0.4px;
      border: 1px solid transparent;
    }

    .badge.arch, .badge.use { color: var(--blue); border-color: rgba(43,124,211,0.28); background: rgba(43,124,211,0.10); }
    .badge.act { color: var(--green); border-color: rgba(45,147,100,0.28); background: rgba(45,147,100,0.10); }
    .badge.seq { color: var(--amber); border-color: rgba(190,123,33,0.28); background: rgba(190,123,33,0.10); }
    .badge.state { color: var(--purple); border-color: rgba(122,77,180,0.28); background: rgba(122,77,180,0.10); }
    .badge.erd { color: #8f6200; border-color: rgba(143,98,0,0.28); background: rgba(143,98,0,0.10); }
    .badge.note { color: #365978; border-color: rgba(54,89,120,0.20); background: rgba(54,89,120,0.08); }

    ul.flat {
      margin: 0;
      padding-left: 18px;
    }

    ul.flat li + li {
      margin-top: 8px;
    }

    .diagram {
      padding: 24px;
      overflow-y: auto;
      overflow-x: auto;
      scrollbar-gutter: stable both-edges;
      overscroll-behavior: contain;
      background:
        linear-gradient(180deg, rgba(248, 251, 255, 0.75), rgba(255, 255, 255, 0.98)),
        repeating-linear-gradient(
          90deg,
          rgba(43,124,211,0.035) 0,
          rgba(43,124,211,0.035) 1px,
          transparent 1px,
          transparent 28px
        );
    }

    .diagram[data-engine-pane="plantuml"] { display: none; }
    body[data-engine="plantuml"] .diagram[data-engine-pane="mermaid"] { display: none; }
    body[data-engine="plantuml"] .diagram[data-engine-pane="plantuml"] { display: block; }

    .diagram.mermaid { min-width: 720px; }
    .diagram svg,
    .diagram img { display: block; height: auto; max-width: none; }

    .card.arch-wide .diagram.mermaid { min-width: 1780px; }
    .card.flow-wide .diagram.mermaid { min-width: 1480px; }
    .card.flow-xwide .diagram.mermaid { min-width: 1920px; }
    .card.sequence-wide .diagram.mermaid { min-width: 2050px; }
    .card.sequence-xwide .diagram.mermaid { min-width: 2620px; }
    .card.state-wide .diagram.mermaid { min-width: 1180px; }
    .card.erd-wide .diagram.mermaid { min-width: 3600px; }
    .card.erd-wide .diagram {
      height: min(78vh, 1100px);
      min-height: 720px;
    }
    body[data-engine="mermaid"] .card.erd-wide .scroll-slider-wrap {
      display: flex;
    }
    .card.erd-wide .diagram[data-engine-pane="mermaid"] {
      width: 100%;
      max-width: 100%;
      overflow-x: scroll;
    }
    .card.erd-wide .diagram[data-engine-pane="mermaid"] > svg,
    .card.erd-wide .diagram[data-engine-pane="mermaid"] > div,
    .card.erd-wide .diagram[data-engine-pane="mermaid"] > pre {
      width: max-content !important;
      min-width: 3600px;
      max-width: none !important;
    }

    code {
      font-family: "Consolas", "SFMono-Regular", monospace;
      background: rgba(25, 50, 76, 0.06);
      padding: 1px 5px;
      border-radius: 6px;
    }

    @media (max-width: 900px) {
      .engine-bar { top: 54px; }
      .engine-bar-inner { align-items: flex-start; }
      .diagram { padding: 16px; }
      .card.erd-wide .diagram {
        height: 72vh;
        min-height: 520px;
      }
    }
  </style>
</head>
<body data-engine="mermaid">
  <header class="hero">
    <h1>Diagram quản lý hàng hóa và bảo hành</h1>
    <p>Bản render này được dựng lại từ source Mermaid và PlantUML mới nhất. Nó đã đồng bộ các thay đổi về <code>Warehouse</code> mặc định ẩn UI, import tồn đầu kỳ từ <code>Excel/CSV</code> qua <code>StockIn OpeningBalance</code>, và thuế cơ bản ở mức hóa đơn với <code>SubTotal</code>, <code>TaxAmount</code>, <code>GrandTotal</code>.</p>
  </header>

  <nav>
    $navHtml
  </nav>

  <section class="engine-bar">
    <div class="engine-bar-inner">
      <div class="engine-copy">
        <strong>Engine render</strong>
        <span>Chuyển toàn bộ sơ đồ giữa Mermaid và PlantUML. Hai engine cùng bám source đã cập nhật, không vá tay riêng lẻ trên bản HTML.</span>
      </div>
      <div class="engine-toggle" role="tablist" aria-label="Diagram engine">
        <button type="button" class="engine-btn is-active" data-engine-option="mermaid" aria-pressed="true">Mermaid</button>
        <button type="button" class="engine-btn" data-engine-option="plantuml" aria-pressed="false">PlantUML</button>
      </div>
    </div>
  </section>

  <main>
$decisionsHtml
$bodySections
  </main>

  <script>
    function normalizeMermaidDiagramWidths() {
      document.querySelectorAll('.diagram[data-engine-pane="mermaid"] svg').forEach((svg) => {
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
      document.querySelectorAll('.diagram[data-engine-pane="mermaid"]').forEach((pane) => {
        observer.observe(pane, { childList: true, subtree: true });
      });
    }

    function syncErdScrollControls() {
      document.querySelectorAll('.card.erd-wide').forEach((card) => {
        const sliderWrap = card.querySelector('[data-scroll-slider-wrap]');
        const slider = sliderWrap && sliderWrap.querySelector('[data-scroll-slider]');
        const diagram = card.querySelector('.diagram[data-engine-pane="mermaid"]');
        if (!sliderWrap || !slider || !diagram) return;

        const refresh = () => {
          const max = Math.max(diagram.scrollWidth - diagram.clientWidth, 0);
          slider.max = String(max);
          slider.value = String(Math.min(diagram.scrollLeft, max));
          slider.disabled = max <= 0;
        };

        if (!slider.dataset.bound) {
          slider.addEventListener('input', () => {
            diagram.scrollLeft = Number(slider.value);
          });
          diagram.addEventListener('scroll', () => {
            slider.value = String(diagram.scrollLeft);
          });
          slider.dataset.bound = 'true';
        }

        refresh();
      });
    }

    mermaid.initialize({
      startOnLoad: true,
      theme: "base",
      securityLevel: "loose",
      flowchart: { htmlLabels: true, curve: "basis" },
      themeVariables: {
        primaryColor: "#f8fbff",
        primaryTextColor: "#19324c",
        primaryBorderColor: "#2b7cd3",
        lineColor: "#5d7288",
        secondaryColor: "#eef5fc",
        tertiaryColor: "#ffffff",
        fontFamily: "Segoe UI, Noto Sans, sans-serif",
        noteBkgColor: "#fff8e8",
        noteBorderColor: "#be7b21",
        clusterBkg: "#f7fbff",
        clusterBorder: "#bfd1e3"
      }
    });

    const buttons = document.querySelectorAll("[data-engine-option]");
    buttons.forEach((button) => {
      button.addEventListener("click", () => {
        const engine = button.getAttribute("data-engine-option");
        document.body.setAttribute("data-engine", engine);
        buttons.forEach((item) => {
          const active = item === button;
          item.classList.toggle("is-active", active);
          item.setAttribute("aria-pressed", String(active));
        });
        if (engine === "mermaid") {
          window.setTimeout(() => {
            normalizeMermaidDiagramWidths();
            syncErdScrollControls();
          }, 0);
        }
      });
    });

    window.addEventListener("load", () => {
      watchMermaidDiagramWidths();
      window.setTimeout(() => {
        normalizeMermaidDiagramWidths();
        syncErdScrollControls();
      }, 150);
    });
  </script>
</body>
</html>
"@

Set-Content -Encoding UTF8 $outputPath $html

