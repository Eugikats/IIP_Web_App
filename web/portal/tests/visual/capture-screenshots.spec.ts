import { test, expect } from '@playwright/test';
import fs from 'fs';
import path from 'path';

test('capture all routes for all roles', async ({ page }) => {
  test.setTimeout(300000); // 5 minutes
  const baseUrl = 'http://localhost:3000';
  
  // Set auth cookie for DevelopmentAuth mode to ensure we bypass login
  await page.context().addCookies([{
    name: 'iip_dev_auth',
    value: 'true',
    domain: 'localhost',
    path: '/',
  }]);

  const roles = ['Handler', 'Supervisor', 'Exec', 'Admin'];

  const routes = [
    { path: '/', name: '01-command-center' },
    { path: '/claims', name: '02-claims-inbox' },
    { path: '/claims/CLM-2024-001', name: '03-case-review' },
    { path: '/claims/new', name: '04-new-claim' },
    { path: '/document-checker', name: '05-document-checker' },
    { path: '/ai-assistant', name: '06-ai-conversations' },
    { path: '/knowledge', name: '07-knowledge-hub' },
    { path: '/approvals', name: '08-workflow-approvals' },
    { path: '/customer-service', name: '09-customer-service' },
    { path: '/integrations', name: '10-integrations' },
    { path: '/reports', name: '11-reports-audit' },
    { path: '/compliance', name: '12-compliance' },
  ];

  const baseOutDir = path.join(process.cwd(), '..', '..', 'docs', 'visual-guides', 'screenshots');

  for (const role of roles) {
    console.log(`\n=== Capturing role: ${role} ===`);
    const roleDir = path.join(baseOutDir, role.toLowerCase());
    if (!fs.existsSync(roleDir)) {
      fs.mkdirSync(roleDir, { recursive: true });
    }

    // Set role in localStorage
    await page.goto(baseUrl);
    await page.evaluate((r) => {
      window.localStorage.setItem('iip-demo-role', r);
    }, role);
    
    // Reload to apply role
    await page.reload();
    await page.waitForTimeout(1000);

    for (const route of routes) {
      console.log(`Navigating to ${route.path}...`);
      await page.goto(`${baseUrl}${route.path}`);
      
      // Wait for the main content to settle
      await page.waitForTimeout(2000); 
      
      const screenshotPath = path.join(roleDir, `${route.name}.png`);
      await page.screenshot({ path: screenshotPath, fullPage: true });
      console.log(`Saved screenshot: ${screenshotPath}`);
    }
  }
});
