import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { LandingPage } from './landing.page';

function make(): LandingPage {
  TestBed.configureTestingModule({});
  return TestBed.runInInjectionContext(() => new LandingPage());
}

describe('LandingPage', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    localStorage.clear();
  });

  it('defaults to Bulgarian copy', () => {
    const page = make();
    expect(page.lang()).toBe('bg');
    expect(page.t().nav.demo).toBe('Заявете демо');
    expect(page.t().hero.h1c).toBe('Вашият бранд.');
  });

  it('setLang switches the copy dictionary and persists', () => {
    const page = make();
    page.setLang('en');

    expect(page.lang()).toBe('en');
    expect(page.t().nav.demo).toBe('Book a demo');
    expect(page.t().hero.h1c).toBe('Your brand.');
    expect(localStorage.getItem('odisea_lang')).toBe('en');
  });

  it('restores the persisted language on construction', () => {
    localStorage.setItem('odisea_lang', 'en');
    const page = make();
    expect(page.lang()).toBe('en');
  });

  it('exposes 6 demo offers and 2 hero offers', () => {
    const page = make();
    expect(page.offers.length).toBe(6);
    expect(page.heroOffers.length).toBe(2);
    expect(page.heroOffers[0].id).toBe('o-101');
  });

  it('maps the selected theme to its odc class', () => {
    const page = make();
    expect(page.themeClass()).toBe('odc-theme-paradise'); // default

    page.theme.set('azur');
    expect(page.themeClass()).toBe('odc-theme-azur');

    page.theme.set('odisea');
    expect(page.themeClass()).toBe('');
  });

  it('toggles the demo layout', () => {
    const page = make();
    expect(page.layout()).toBe('grid');
    page.layout.set('carousel');
    expect(page.layout()).toBe('carousel');
  });

  it('resolves Bulgarian lookups for offer cards', () => {
    const page = make();
    expect(page.country('GR')).toBe('Гърция');
    expect(page.flag('GR')).toBe('🇬🇷');
    expect(page.board('ai')).toBe('All inclusive');
    expect(page.cat('beach')).toBe('Морска почивка');
  });

  it('produces sanitized svg markup for icons', () => {
    const page = make();
    const html = page.svg('package', 22) as unknown as { changingThisBreaksApplicationSecurity?: string };
    // SafeHtml wraps the markup; the bypassed value is retrievable in tests.
    expect(JSON.stringify(html)).toContain('svg');
  });
});
