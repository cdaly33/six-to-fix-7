// Enhanced Playwright capture script — handles login properly
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
    console.log(`[${prefix}] hero bg (first 120): ${String(heroBg).substring(0, 120)}`);

    // Login page
    await page.goto(BASE_URL + '/login', { waitUntil: 'networkidle', timeout: 30000 });
    await page.screenshot({ path: path.join(__dirname, `${prefix}-login.png`), fullPage: true });

    // Try to login
    try {
        const emailInput = await page.waitForSelector('input[type="email"], input[id*="email" i], input[name*="email" i]', { timeout: 5000 });
        await emailInput.fill(EMAIL);
        const pwInput = await page.waitForSelector('input[type="password"]', { timeout: 5000 });
        await pwInput.fill(PASSWORD);

        // Find and click submit
        const submitBtn = await page.$('button[type="submit"]') ||
                          await page.$('input[type="submit"]') ||
                          await page.$('button:has-text("Sign In")') ||
                          await page.$('button:has-text("Login")');
        if (submitBtn) await submitBtn.click();
        else await page.keyboard.press('Enter');

        await page.waitForTimeout(4000);
        console.log(`[${prefix}] After login, URL: ${page.url()}`);
    } catch (e) {
        console.log(`[${prefix}] Login attempt: ${e.message}`);
    }

    // Navigate to dashboard
    await page.goto(BASE_URL + '/dashboard', { waitUntil: 'networkidle', timeout: 30000 });
    const dashUrl = page.url();
    console.log(`[${prefix}] Dashboard URL after nav: ${dashUrl}`);
    await page.screenshot({ path: path.join(__dirname, `${prefix}-dashboard.png`), fullPage: true });

    // Check sidebar
    const sidebarBg = await page.$eval('.section-sidebar', el => window.getComputedStyle(el).backgroundColor).catch(() => 'NOT_FOUND');
    const sidebarWidth = await page.$eval('.section-sidebar', el => window.getComputedStyle(el).width).catch(() => 'NOT_FOUND');
    const sidebarDisplay = await page.$eval('.section-sidebar', el => window.getComputedStyle(el).display).catch(() => 'NOT_FOUND');
    console.log(`[${prefix}] sidebar bg: ${sidebarBg}`);
    console.log(`[${prefix}] sidebar width: ${sidebarWidth}`);
    console.log(`[${prefix}] sidebar display: ${sidebarDisplay}`);

    // Check if styles.css loaded
    const stylesLoaded = await page.evaluate(() => {
        const links = Array.from(document.querySelectorAll('link[rel="stylesheet"]'));
        return links.map(l => l.href);
    });
    console.log(`[${prefix}] CSS links:`, stylesLoaded);

    console.log(`[${prefix}] 404s on CSS/JS:`, failed404s);
    await browser.close();
}

const prefix = process.argv[2] || 'check';
capture(prefix).catch(err => { console.error(err); process.exit(1); });
