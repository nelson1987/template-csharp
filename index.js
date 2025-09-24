import getHealth from "./tst/TemplateProject.Performance/get-health.js";
// import getWeatherforecast from "./scenarios/get-weatherforecast.js";
import { group, sleep } from "k6";

export default () => {
    group("Endpoint [GET] /health", () => {
        getHealth();
    });
    sleep(1);
    
    // group("Endpoint [GET] /ping", () => {
    //     getWeatherforecast();
    // });
    // sleep(1);
};