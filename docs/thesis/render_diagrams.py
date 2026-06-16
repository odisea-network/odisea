# -*- coding: utf-8 -*-
"""Рендира всички diagrams/*.mmd до PNG чрез локален mermaid и Playwright."""
import os, glob
from playwright.sync_api import sync_playwright

HERE = os.path.dirname(os.path.abspath(__file__))
MERMAID = os.path.join(HERE, "assets", "mermaid.min.js")
DIA = os.path.join(HERE, "diagrams")

HTML = """<!doctype html><html><head><meta charset="utf-8">
<style>
  body {{ margin:0; padding:20px; background:#ffffff;
         font-family:'Segoe UI', Tahoma, sans-serif; }}
  .mermaid {{ font-size:15px; }}
</style></head><body>
<div class="mermaid">
{src}
</div>
<script>{lib}</script>
<script>
  mermaid.initialize({{ startOnLoad:false, theme:'neutral', securityLevel:'loose',
    flowchart:{{ useMaxWidth:false, htmlLabels:true, curve:'basis' }},
    er:{{ useMaxWidth:false }}, sequence:{{ useMaxWidth:false }} }});
  window.__ok = false;
  mermaid.run().then(() => {{ window.__ok = true; }})
              .catch(e => {{ document.title = 'ERR:' + e.message; window.__ok = 'err'; }});
</script></body></html>"""


def render():
    lib = open(MERMAID, encoding="utf-8").read()
    files = sorted(glob.glob(os.path.join(DIA, "*.mmd")))
    if not files:
        print("Няма .mmd файлове в", DIA); return
    with sync_playwright() as p:
        browser = p.chromium.launch()
        ctx = browser.new_context(device_scale_factor=2, viewport={"width": 1600, "height": 1200})
        for f in files:
            name = os.path.splitext(os.path.basename(f))[0]
            src = open(f, encoding="utf-8").read()
            page = ctx.new_page()
            page.set_content(HTML.format(src=src, lib=lib), wait_until="load")
            try:
                page.wait_for_function("window.__ok === true", timeout=20000)
            except Exception:
                print("ГРЕШКА при", name, "->", page.title()); page.close(); continue
            el = page.query_selector(".mermaid svg")
            out = os.path.join(DIA, name + ".png")
            el.screenshot(path=out)
            print("OK", name + ".png")
            page.close()
        browser.close()


if __name__ == "__main__":
    render()
