# -*- coding: utf-8 -*-
"""Прави екранни снимки от работещото приложение (localhost) чрез Playwright."""
import os
from playwright.sync_api import sync_playwright

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "screenshots")
BASE = "http://localhost:4200"
os.makedirs(OUT, exist_ok=True)

ANON = [("/", "landing"), ("/login", "login"), ("/register", "register")]
OPERATOR = [("/operator/offers", "operator-offers"), ("/operator/connections", "connections")]
AGENCY = [("/collections", "collections"), ("/builder", "builder"),
          ("/marketplace", "marketplace"), ("/leads", "leads"), ("/webhooks", "webhooks")]


def shoot(page, path, name):
    page.goto(BASE + path, wait_until="networkidle")
    page.wait_for_timeout(1200)
    page.screenshot(path=os.path.join(OUT, name + ".png"))
    print("OK", name)


def login(page, email, password):
    page.goto(BASE + "/login", wait_until="networkidle")
    page.fill("input[name=email]", email)
    page.fill("input[name=password]", password)
    page.click("form button[type=submit]")
    try:
        page.wait_for_url("**/offers", timeout=15000)
    except Exception:
        page.wait_for_timeout(2000)


def run():
    with sync_playwright() as p:
        browser = p.chromium.launch()
        # анонимни екрани
        ctx = browser.new_context(viewport={"width": 1440, "height": 900}, device_scale_factor=1.5)
        pg = ctx.new_page()
        for path, name in ANON:
            shoot(pg, path, name)
        ctx.close()

        # оператор
        ctx = browser.new_context(viewport={"width": 1440, "height": 900}, device_scale_factor=1.5)
        pg = ctx.new_page()
        login(pg, "ops@sun-operators.com", "Ops1234!")
        for path, name in OPERATOR:
            shoot(pg, path, name)
        ctx.close()

        # агенция
        ctx = browser.new_context(viewport={"width": 1440, "height": 900}, device_scale_factor=1.5)
        pg = ctx.new_page()
        login(pg, "blue@blue-horizon.com", "Blue1234!")
        for path, name in AGENCY:
            shoot(pg, path, name)
        ctx.close()
        browser.close()


if __name__ == "__main__":
    run()
