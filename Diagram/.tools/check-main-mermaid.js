const fs = require('fs');
const mermaid = require('mermaid');
const md = fs.readFileSync('Diagram/warehouse_management_vi_senior_reviewed_mermaid.md', 'utf8');
const matches = [...md.matchAll(/^##\s+(.+?)\r?\n([\s\S]*?)```mermaid\r?\n([\s\S]*?)```/gm)];
let i = 0;
(async () => {
  mermaid.initialize({ startOnLoad:false, securityLevel:'loose', theme:'base' });
  for (const m of matches) {
    i++;
    const title = m[1].trim();
    const code = m[3];
    try {
      await mermaid.parse(code);
      console.log(`OK ${i}: ${title}`);
    } catch (e) {
      console.log(`FAIL ${i}: ${title}`);
      console.log(String(e).slice(0,500));
    }
  }
})();
