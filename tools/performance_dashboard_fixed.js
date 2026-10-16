import http from 'k6/http';
import { check, sleep } from 'k6';

const baseUrl = __ENV.K6_BASE_URL || 'https://localhost:7143';

export const options = {
  vus: 5,
  duration: '30s',
  thresholds: {
    'http_req_failed{endpoint:dashboard}': ['rate<0.01'],
    'http_req_duration{endpoint:dashboard}': ['p(95)<2000'],
    'checks{endpoint:dashboard}': ['rate>0.99'],
  },
};

export function setup() {
  if (!__ENV.K6_EMAIL || !__ENV.K6_PASSWORD) {
    throw new Error('Requester email and password are required.');
  }

  const page = http.get(`${baseUrl}/Account/Login`);

  const token = page.html()
    .find('input[name="__RequestVerificationToken"]')
    .first()
    .attr('value');

  if (!token) {
    throw new Error('Login security token was not found.');
  }

  const login = http.post(`${baseUrl}/Account/Login`, {
    Email: __ENV.K6_EMAIL,
    Password: __ENV.K6_PASSWORD,
    __RequestVerificationToken: token,
  }, {
    redirects: 0,
  });

  if (login.status !== 302) {
    throw new Error('Login failed. Performance test stopped.');
  }

  const cookies = login.cookies['.AspNetCore.Identity.Application'];

  if (!cookies || cookies.length === 0) {
    throw new Error('Authentication cookie was not received.');
  }

  return {
    sessionCookie: `.AspNetCore.Identity.Application=${cookies[0].value}`,
  };
}

export default function (data) {
  const response = http.get(`${baseUrl}/Requests`, {
    headers: {
      Cookie: data.sessionCookie,
    },
    redirects: 0,
    tags: {
      endpoint: 'dashboard',
    },
  });

  check(response, {
    'dashboard returned 200': (r) => r.status === 200,
    'dashboard content displayed': (r) =>
      r.body.includes('My service requests'),
  }, {
    endpoint: 'dashboard',
  });

  sleep(1);
}