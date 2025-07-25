import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
  vus: 20,          // 20 concurrent users
  duration: '1m',   // Run for 1 minute
};

export default function () {
  const BASE_URL = 'http://localhost:5254/api/WebpageAnalyse';
  const email = 'nithinsabus@gmail.com';

  // -------- LOGIN --------
  let loginRes = http.post(`${BASE_URL}/login?email=${encodeURIComponent(email)}`);
  check(loginRes, {
    'login status is 200': (r) => r.status === 200,
  });

  // -------- LIST WEBPAGES --------
  let listRes = http.get(`${BASE_URL}/list-webpages?email=${encodeURIComponent(email)}`);
  check(listRes, {
    'list status is 200': (r) => r.status === 200,
  });

  // -------- VIEW FIRST WEBPAGE IF ANY --------
  if (listRes.status === 200) {
    let webpages = listRes.json();
    if (webpages.length > 0) {
      let firstWebpageId = webpages[0].id;
      let viewRes = http.get(`${BASE_URL}/view-webpage/${firstWebpageId}?email=${encodeURIComponent(email)}`);
      check(viewRes, {
        'view status is 200': (r) => r.status === 200,
      });
    }
  }

  sleep(1); // Sleep to simulate real user pacing
}
