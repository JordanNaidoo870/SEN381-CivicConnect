// k6 -- requester's authenticated dashboard, no credentials stored in Git.
// PowerShell example:
// $env:K6_BASE_URL='https://localhost:7143'; $env:K6_EMAIL='...'; $env:K6_PASSWORD='...'; k6 run tools/performance_dashboard.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 5,
  duration: '30s',
  thresholds: { http_req_failed: ['rate<0.01'] }
};
const base = __ENV.K6_BASE_URL || 'https://localhost:7143';
export function setup() {
  if (!__ENV.K6_EMAIL || !__ENV.K6_PASSWORD) throw new Error('Set K6_EMAIL and K6_PASSWORD');
}
export default function () {
  const page = http.get(`${base}/Account/Login`);
  const token = page.html().find('input[name="__RequestVerificationToken"]').first().attr('value');
  const login = http.post(`${base}/Account/Login`, {
    Email: __ENV.K6_EMAIL, Password: __ENV.K6_PASSWORD,
    __RequestVerificationToken: token
  }, { redirects: 0 });
  check(login, { 'login accepted': r => r.status === 302 });
  const dashboard = http.get(`${base}/Requests`);
  check(dashboard, { 'dashboard 200': r => r.status === 200, 'contains dashboard': r => r.body.includes('My service requests') });
  sleep(1);
}
