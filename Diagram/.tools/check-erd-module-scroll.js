const { chromium } = require('playwright');
const path = require('path');
(async() => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1200 } });
  const fileUrl = 'file:///' + path.resolve('Diagram/ERD module.html').replace(/\\/g,'/');
  await page.goto(fileUrl, { waitUntil: 'load' });
  await page.waitForTimeout(4000);
  const data = await page.evaluate(() => {
    const card = document.querySelector('.diagram-wrap.mermaid-pane');
    if (!card) return { found: false };
    const svg = card.querySelector('svg');
    return {
      found: true,
      clientWidth: card.clientWidth,
      scrollWidth: card.scrollWidth,
      overflowX: getComputedStyle(card).overflowX,
      svgWidthStyle: svg ? svg.style.width : null,
      svgRectWidth: svg ? svg.getBoundingClientRect().width : null,
      hasScrollbar: card.scrollWidth > card.clientWidth
    };
  });
  console.log(JSON.stringify(data, null, 2));
  await browser.close();
})();
