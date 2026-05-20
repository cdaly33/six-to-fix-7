// Blazor-aware Playwright login script
const { chromium } = require('@playwright/test');
const path = require('path');

const BASE_URL = 'https://app-sixtofix-prod.azurewebsites.net';
const EMAIL = 'chris@christopherdaly.com';
const PASSWORD = 'GYyE3jnmvGJuMyjtNQAk!';

async function capture(prefix) {
    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();

    const failed404s = [];
    page.on('response', res => {
        if (res.status() === 404 && (res.url().includes('.css') || res.url().includes('.js') || res.url().includes('.styles')))
            failed404s.push(res.url());
    });

    // Homepage
    await page.goto(BASE_URL + '/', { waitUntil: 'networkidle', timeout: 30000 });
    await page.screenshot({ path: path.join(__dirname, `${prefix}-home.png`), fullPage: true });
    const heroH1Color = await page.$eval('#hero-heading', el => window.getComputedStyle(el).color).catch(() => 'N/A');
    const heroBg = await page.$eval('.hero', el => window.getComputedStyle(el).background).catch(() => 'N/A');
    console.log(`[${prefix}] hero H1 color: ${heroH1Color}`);
    console.log(`[${prefix}] hero bg (140): ${String(heroBg).substring(0, 140)}`);

    // Login — wait for Blazor interactive to be ready
    await page.goto(BASE_URL + '/login', { waitUntil: 'networkidle', timeout: 30000 });
    // Wait for Blazor to boot (the button should become enabled)
    await page.waitForSelector('button[type="submit"]:not([disabled])', { timeout: 15000 }).catch(() => {
        console.log(`[${prefix}] Warning: submit btn not enabled yet, trying anyway`);
    });
    await page.screenshot({ path: path.join(__dirname, `${prefix}-login.png`), fullPage: true });

    // Fill form — Blazor InputText uses native input but Blazor may need the change event
    await page.locator('#email').fill(EMAIL);
    await page.locator('#email').press('Tab');
    await page.locator('#password').fill(PASSWORD);
    await page.locator('#password').press('Tab');
    await page.waitForTimeout(500);
    await page.screenshot({ path: path.join(__dirname, `${prefix}-login-filled.png`), fullPage: false });
    
    await page.click('button[type="submit"]');
    // Wait for navigation or error
    await Promise.race([
        page.waitForURL('**/dashboard**', { timeout: 10000 }),
        page.waitForSelector('.alert-error', { timeout: 10000 }),
        page.waitForTimeout(10000)
    ]).catch(() => {});

    console.log(`[${prefix}] After login, URL: ${page.url()}`);

    if (!page.url().includes('/dashboard')) {
        // Check for error message
        const errorText = await page.$eval('.alert-error', el => el.textContent).catch(() => null);
        console.log(`[${prefix}] Login error message: ${errorText}`);
        // Try direct navigation to dashboard anyway
        await page.goto(BASE_URL + '/dashboard', { waitUntil: 'networkidle', timeout: 30000 });
    }

    await page.waitForTimeout(2000);
    console.log(`[${prefix}] Dashboard URL: ${page.url()}`);
    await page.screenshot({ path: path.join(__dirname, `${prefix}-dashboard.png`), fullPage: true });

    // Check sidebar computed styles
    const sidebarBg = await page.$eval('.section-sidebar', el => window.getComputedStyle(el).backgroundColor).catch(() => 'NOT_FOUND');
    const sidebarWidth = await page.$eval('.section-sidebar', el => window.getComputedStyle(el).width).catch(() => 'NOT_FOUND');
    const shShell = await page.$eval('.sh-shell', el => window.getComputedStyle(el).display).catch(() => 'NOT_FOUND');
    console.log(`[${prefix}] sidebar bg: ${sidebarBg}`);
    console.log(`[${prefix}] sidebar width: ${sidebarWidth}`);
    console.log(`[${prefix}] sh-shell display: ${shShell}`);

    // CSS links loaded
    const cssLinks = await page.evaluate(() =>
        Array.from(document.querySelectorAll('link[rel="stylesheet"]')).map(l => l.href.replace(/.*\/(css|SixToFix)/, '...$1'))
    );
    console.log(`[${prefix}] CSS links:`, cssLinks);
    console.log(`[${prefix}] 404s:`, failed404s);
    await browser.close();
}

const prefix = process.argv[2] || 'check';
capture(prefix).catch(err => { console.error(err); process.exit(1); });
