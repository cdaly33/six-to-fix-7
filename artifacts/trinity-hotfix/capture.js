// Playwright capture script for StrategyHub before/after screenshots
const { chromium } = require('@playwright/test');
const path = require('path');

const BASE_URL = 'https://app-sixtofix-prod.azurewebsites.net';
const EMAIL = 'chris@christopherdaly.com';
const PASSWORD = 'GYyE3jnmvGJuMyjtNQAk!';

async function capture(prefix) {
    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();

    const errors = [];
    page.on('console', msg => { if (msg.type() === 'error') errors.push(msg.text()); });
    const failed404s = [];
    page.on('response', res => { if (res.status() === 404 && (res.url().includes('.css') || res.url().includes('.js'))) failed404s.push(res.url()); });

    // Homepage
    await page.goto(BASE_URL + '/', { waitUntil: 'networkidle', timeout: 30000 });
    await page.screenshot({ path: path.join(__dirname, `${prefix}-home.png`), fullPage: true });

    // Hero H1 computed styles
    const heroH1Color = await page.$eval('#hero-heading', el => window.getComputedStyle(el).color).catch(() => 'N/A');
    const heroBg = await page.$eval('.hero', el => window.getComputedStyle(el).background).catch(() => 'N/A');
    const heroH1Classes = await page.$eval('#hero-heading', el => el.className).catch(() => 'N/A');
    console.log(`[${prefix}] hero H1 color: ${heroH1Color}`);
    console.log(`[${prefix}] hero bg: ${heroBg.substring(0, 200)}`);
    console.log(`[${prefix}] hero H1 classes: ${heroH1Classes}`);

    // Login
    await page.goto(BASE_URL + '/login', { waitUntil: 'networkidle', timeout: 30000 });
    await page.screenshot({ path: path.join(__dirname, `${prefix}-login.png`), fullPage: true });

    // Fill login form
    await page.fill('input[type="email"], input[name="email"], #email', EMAIL).catch(() => {});
    await page.fill('input[type="password"], input[name="password"], #password', PASSWORD).catch(() => {});
    await page.click('button[type="submit"], .btn-primary, button:has-text("Sign In"), button:has-text("Log In")').catch(() => {});
    await page.waitForTimeout(3000);
    
    // Dashboard
    await page.goto(BASE_URL + '/dashboard', { waitUntil: 'networkidle', timeout: 30000 });
    await page.screenshot({ path: path.join(__dirname, `${prefix}-dashboard.png`), fullPage: true });

    // Sidebar computed styles
    const sidebarBg = await page.$eval('.section-sidebar', el => window.getComputedStyle(el).backgroundColor).catch(() => 'N/A');
    const sidebarWidth = await page.$eval('.section-sidebar', el => window.getComputedStyle(el).width).catch(() => 'N/A');
    const sidebarDisplay = await page.$eval('.section-sidebar', el => window.getComputedStyle(el).display).catch(() => 'N/A');
    const sidebarClasses = await page.$eval('.section-sidebar', el => el.className).catch(() => 'N/A');
    console.log(`[${prefix}] sidebar bg: ${sidebarBg}`);
    console.log(`[${prefix}] sidebar width: ${sidebarWidth}`);
    console.log(`[${prefix}] sidebar display: ${sidebarDisplay}`);
    console.log(`[${prefix}] sidebar classes: ${sidebarClasses}`);

    console.log(`[${prefix}] 404s on CSS/JS:`, failed404s);
    console.log(`[${prefix}] console errors:`, errors.slice(0, 5));

    await browser.close();
}

const prefix = process.argv[2] || 'before';
capture(prefix).catch(err => { console.error(err); process.exit(1); });
