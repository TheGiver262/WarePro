const { chromium } = require('playwright');
const path = require('path');
(async() => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1800 } });
  const fileUrl = 'file:///' + path.resolve('Diagram/warehouse_diagrams.html').replace(/\\/g,'/') + '#sequence';
  await page.goto(fileUrl, { waitUntil: 'load' });
  await page.waitForTimeout(4000);
  await page.click('[data-engine-option="plantuml"]');
  await page.waitForTimeout(1000);
  await page.screenshot({ path: 'Diagram/.tools/login-sequence-updated-plantuml.png' });
  await browser.close();
})();
