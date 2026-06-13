import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';

// ── Themeable embed components: demo catalog (subset of the shared catalog) ──────
interface DemoOffer {
  id: string; title: string; country: string; region: string;
  cat: string; board: string; transport: 'plane' | 'bus'; price: number; nights: number; img: string;
}

const IMG = (id: string) => `https://images.unsplash.com/photo-${id}?w=800&q=80&auto=format&fit=crop`;

// Order matches the design's DEMO_IDS. Hero uses the first two.
const DEMO_OFFERS: DemoOffer[] = [
  { id: 'o-101', title: 'Санторини: кикладска магия край калдерата', country: 'GR', region: 'Санторини', cat: 'beach', board: 'bb', transport: 'plane', price: 760, nights: 7, img: IMG('1570077188670-e3a8d69ac5ff') },
  { id: 'o-119', title: 'Закинтос: заливът на корабокрушението', country: 'GR', region: 'Закинтос', cat: 'beach', board: 'ai', transport: 'plane', price: 720, nights: 7, img: IMG('1533105079780-92b9be482077') },
  { id: 'o-114', title: 'Малдиви: вила над водата', country: 'MV', region: 'Мале атол', cat: 'honey', board: 'fb', transport: 'plane', price: 2480, nights: 7, img: IMG('1514282401047-d79a71a590e8') },
  { id: 'o-105', title: 'Кападокия: балони над лунен пейзаж', country: 'TR', region: 'Кападокия', cat: 'tour', board: 'bb', transport: 'plane', price: 720, nights: 5, img: IMG('1531168556467-80aace0d0144') },
  { id: 'o-109', title: 'Амалфи: крайбрежие на лимоните', country: 'IT', region: 'Амалфи', cat: 'tour', board: 'bb', transport: 'plane', price: 980, nights: 6, img: IMG('1534445867742-43195f401b6c') },
  { id: 'o-116', title: 'Дубровник: перлата на Адриатика', country: 'HR', region: 'Дубровник', cat: 'tour', board: 'bb', transport: 'bus', price: 640, nights: 5, img: IMG('1499678329028-101435549a4e') },
];

const COUNTRY: Record<string, { bg: string; flag: string }> = {
  GR: { bg: 'Гърция', flag: '🇬🇷' }, MV: { bg: 'Малдиви', flag: '🇲🇻' }, TR: { bg: 'Турция', flag: '🇹🇷' },
  IT: { bg: 'Италия', flag: '🇮🇹' }, HR: { bg: 'Хърватия', flag: '🇭🇷' },
};
const CAT: Record<string, string> = { beach: 'Морска почивка', honey: 'Меден месец', tour: 'Обиколен тур' };
const BOARD: Record<string, string> = { bb: 'Закуска', ai: 'All inclusive', fb: 'Пълен пансион', hb: 'Полупансион' };

// ── Lucide-style icon paths (24/2/round) ────────────────────────────────────────
const ICON_PATHS: Record<string, string> = {
  package: '<path d="m7.5 4.3 9 5.2M3.3 7 12 12l8.7-5M12 22V12M21 16V8a2 2 0 0 0-1-1.7l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.7l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z"/>',
  sliders: '<path d="M4 21v-7M4 10V3M12 21v-9M12 8V3M20 21v-5M20 12V3"/><path d="M1 14h6M9 8h6M17 16h6"/>',
  code: '<path d="m16 18 6-6-6-6M8 6l-6 6 6 6"/>',
  zap: '<path d="M13 2 3 14h7l-1 8 10-12h-7l1-8Z"/>',
  grid: '<rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/>',
  columns: '<rect x="3" y="3" width="18" height="18" rx="2"/><path d="M12 3v18"/>',
  arrowR: '<path d="M5 12h14M12 5l7 7-7 7"/>',
  eye: '<path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/>',
  check: '<path d="M20 6 9 17l-5-5"/>',
  link: '<path d="M10 13a5 5 0 0 0 7 0l3-3a5 5 0 0 0-7-7l-1.5 1.5"/><path d="M14 11a5 5 0 0 0-7 0l-3 3a5 5 0 0 0 7 7l1.5-1.5"/>',
  ext: '<path d="M15 3h6v6M10 14 21 3M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/>',
  pin: '<path d="M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0Z"/><circle cx="12" cy="10" r="3"/>',
  moon: '<path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"/>',
  plane: '<path d="M17.8 19.2 16 11l3.5-3.5C21 6 21.5 4 21 3c-1-.5-3 0-4.5 1.5L13 8 4.8 6.2c-.5-.1-.9.1-1.1.5l-.3.5c-.2.5-.1 1 .3 1.3L9 12l-2 3H4l-1 1 3 2 2 3 1-1v-3l3-2 3.5 5.3c.3.4.8.5 1.3.3l.5-.2c.4-.3.6-.7.5-1.2z"/>',
  bus: '<path d="M8 6v6M16 6v6M2 12h19.6M18 18h3s.5-1.7.8-2.8c.1-.4.2-.8.2-1.2V7a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2v7c0 1.1.9 2 2 2h2"/><circle cx="7" cy="18" r="2"/><path d="M9 18h5"/><circle cx="16" cy="18" r="2"/>',
  heart: '<path d="M19 14c1.5-1.5 3-3.3 3-5.5A5.5 5.5 0 0 0 12 5 5.5 5.5 0 0 0 2 8.5c0 2.2 1.5 4 3 5.5l7 7Z"/>',
  chevL: '<path d="m15 18-6-6 6-6"/>',
  chevR: '<path d="m9 18 6-6-6-6"/>',
};

// ── Bilingual copy (BG default · EN toggle) ─────────────────────────────────────
const COPY = {
  bg: {
    nav: { how: 'Как работи', channels: 'Канали', operators: 'За оператори', pricing: 'Цени', login: 'Вход', demo: 'Заявете демо' },
    hero: {
      eyebrow: 'Платформа за дистрибуция на туристически продукти',
      h1a: 'Един каталог.', h1b: 'Всеки канал.', h1c: 'Вашият бранд.',
      sub: 'Туроператорите публикуват веднъж. Агенциите избират, стилизират и вграждат офертите навсякъде, с модерни компоненти, които наследяват техния облик.',
      cta1: 'Заявете демо', cta2: 'Вижте демото на живо', trust: 'Доверено от 120+ агенции и оператори',
    },
    values: {
      title: 'Дистрибуция без компромиси',
      sub: 'Не iframe в кутийка. Реални компоненти, реален API, реален контрол върху стила.',
      items: [
        { i: 'package', t: 'Публикувай веднъж, дистрибутирай навсякъде', d: 'Един споделен каталог захранва всичките ви сайтове и партньорски канали едновременно.' },
        { i: 'sliders', t: 'Компонентите наследяват вашия бранд', d: 'Цяла дизайн система чрез CSS променливи, не само цветове. Пътешественикът вижда вас, не нас.' },
        { i: 'code', t: 'Реален API и готови интеграции', d: 'Web компоненти, WordPress, Wix, Shopify и чист REST API с webhooks.' },
        { i: 'zap', t: 'Живи колекции', d: 'Параметризирани селекции, които остават актуални, докато каталогът се обновява.' },
      ],
    },
    how: {
      eyebrow: 'Как работи', title: 'От оферта до вграден компонент за минути',
      steps: [
        { n: '01', t: 'Публикувай оферта', d: 'Качи или синхронизирай оферти в споделения каталог.' },
        { n: '02', t: 'Създай колекция', d: 'Параметризирана селекция: „Гърция, All inclusive, до €800“.' },
        { n: '03', t: 'Стилизирай темата', d: 'Задай токени веднъж, компонентите приемат вашия облик.' },
        { n: '04', t: 'Вземи embed код', d: 'Постави на сайта си. Готово, рендерира се с вашия стил.' },
      ],
    },
    demo: {
      eyebrow: 'Жив преглед', title: 'Това вижда пътешественикът, във вашия стил',
      sub: 'Смени темата и подредбата. Същите компоненти, мигновено пребрандирани.',
      theme: 'Тема', layout: 'Подредба', grid: 'Мрежа', carousel: 'Карусел',
      themes: { odisea: 'Неутрална', paradise: 'Топла', azur: 'Морска' },
      url: 'vashata-agencia.bg/oferti', listTitle: 'Гърция · All inclusive', carouselTitle: 'Препоръчани оферти', credit: 'Задвижвано от Odisea',
    },
    channels: {
      eyebrow: 'Канали и интеграции', title: 'Доставяйте навсякъде, където са клиентите ви',
      sub: 'Един и същ договор за публикуване, различни адаптери.',
      items: [
        { i: 'code', t: 'Web компонент', d: 'Едно <script> и таг', tag: 'Препоръчано' },
        { i: 'package', t: 'WordPress', d: 'Gutenberg блок + shortcode' },
        { i: 'grid', t: 'Wix', d: 'Custom element, без код' },
        { i: 'grid', t: 'Shopify', d: 'Theme app block', soon: true },
        { i: 'columns', t: 'iFrame', d: 'Резервен вариант навсякъде' },
        { i: 'zap', t: 'REST API + webhooks', d: 'Headless, versioned' },
      ],
    },
    operators: {
      eyebrow: 'За туроператори', title: 'Качете офертите си веднъж. Стигнете до десетки агенции.',
      sub: 'Свързваме се с вашата система чрез XML, JSON, CSV или ръчно въвеждане. Нормализираме всяка оферта и я правим достъпна за цялата мрежа от агенции, със запазен произход и проследимост.',
      adapters: ['XML емисия', 'JSON API', 'CSV / SFTP', 'Ръчно въвеждане'],
      bullets: ['Автоматична синхронизация с retry и известия', 'Преглед преди активиране', 'Проследимост на всяка оферта до източника'],
      cta: 'Станете доставчик',
      stats: [{ v: '100+', l: 'оператора в мрежата' }, { v: '3 200+', l: 'активни оферти' }, { v: '12', l: 'мин. средна свежест' }],
    },
    proof: {
      quote: '„Спряхме да поддържаме таблици и остарели iframe-и. Публикуваме веднъж и офертите се появяват на всичките ни сайтове, с нашия бранд.“',
      author: 'Мария Добрева', role: 'Управител, Слънчев Тур',
      logos: ['Слънчев Тур', 'Нова Travel', 'Балкан Холидейс', 'Адриатик', 'Зенит', 'Профи Турс'],
    },
    pricing: {
      eyebrow: 'Цени', title: 'Прозрачно ценообразуване, което расте с вас',
      sub: 'Без скрити такси за интеграция. Откажете по всяко време.', perMonth: '/ мес', popular: 'Популярен',
      tiers: [
        { name: 'Старт', price: '€0', who: 'За малки агенции в начален етап', cta: 'Започнете безплатно', popular: false, feats: ['1 активна колекция', 'Основни компоненти', 'Embed чрез script или iframe', 'Стилизиране с цветове'] },
        { name: 'Pro', price: '€49', who: 'За растящи агенции с няколко сайта', cta: 'Заявете демо', popular: true, feats: ['Неограничени колекции', 'Всички канали (WP, Wix, API)', 'Пълна система от теми', 'Запитвания и анализ', 'API ключ'] },
        { name: 'Enterprise', price: 'По договаряне', who: 'За платформи и големи оператори', cta: 'Свържете се', popular: false, feats: ['REST API + webhooks', 'SEO публикуване', 'Собствени конектори', 'Приоритетна поддръжка и SLA'] },
      ],
    },
    finalCta: { title: 'Готови да публикувате навсякъде?', sub: 'Вижте Odisea с вашите оферти. 30 минути, без ангажимент.', cta: 'Заявете демо', cta2: 'Разгледай портала' },
    footer: { tagline: 'Платформа за дистрибуция и представяне на туристически продукти.', rights: '© 2026 Odisea. Всички права запазени.', cols: [['Продукт', ['Как работи', 'Канали', 'Цени', 'API']], ['Компания', ['За оператори', 'Blog', 'За нас', 'Careers']], ['Правни', ['Поверителност', 'Условия', 'WCAG AA', 'GDPR']]] as [string, string[]][] },
  },
  en: {
    nav: { how: 'How it works', channels: 'Channels', operators: 'For operators', pricing: 'Pricing', login: 'Log in', demo: 'Book a demo' },
    hero: {
      eyebrow: 'Travel product distribution platform',
      h1a: 'One catalog.', h1b: 'Every channel.', h1c: 'Your brand.',
      sub: 'Operators publish once. Agencies curate, style and embed offers anywhere, with modern components that inherit their look.',
      cta1: 'Book a demo', cta2: 'See the live demo', trust: 'Trusted by 120+ agencies and operators',
    },
    values: {
      title: 'Distribution without compromise',
      sub: 'Not an iframe in a box. Real components, a real API, real control over style.',
      items: [
        { i: 'package', t: 'Publish once, distribute everywhere', d: 'One shared catalog powers all your sites and partner channels at once.' },
        { i: 'sliders', t: 'Components inherit your brand', d: 'A whole design system via CSS variables, not just colors. Travelers see you, not us.' },
        { i: 'code', t: 'A real API and drop-in integrations', d: 'Web components, WordPress, Wix, Shopify and a clean REST API with webhooks.' },
        { i: 'zap', t: 'Living collections', d: 'Parameterized selections that stay current as the catalog updates.' },
      ],
    },
    how: {
      eyebrow: 'How it works', title: 'From offer to embedded component in minutes',
      steps: [
        { n: '01', t: 'Publish an offer', d: 'Upload or sync offers into the shared catalog.' },
        { n: '02', t: 'Build a collection', d: "A parameterized selection: 'Greece, all-inclusive, under €800'." },
        { n: '03', t: 'Style the theme', d: 'Set tokens once, components adopt your look.' },
        { n: '04', t: 'Get the embed code', d: 'Paste it on your site. Done, it renders in your style.' },
      ],
    },
    demo: {
      eyebrow: 'Live preview', title: 'This is what the traveler sees, in your style',
      sub: 'Switch theme and layout. Same components, instantly rebranded.',
      theme: 'Theme', layout: 'Layout', grid: 'Grid', carousel: 'Carousel',
      themes: { odisea: 'Neutral', paradise: 'Warm', azur: 'Coastal' },
      url: 'your-agency.com/offers', listTitle: 'Greece · all-inclusive', carouselTitle: 'Featured offers', credit: 'Powered by Odisea',
    },
    channels: {
      eyebrow: 'Channels & integrations', title: 'Deliver wherever your customers are',
      sub: 'One publishing contract, many adapters.',
      items: [
        { i: 'code', t: 'Web component', d: 'One <script> and a tag', tag: 'Recommended' },
        { i: 'package', t: 'WordPress', d: 'Gutenberg block + shortcode' },
        { i: 'grid', t: 'Wix', d: 'Custom element, no code' },
        { i: 'grid', t: 'Shopify', d: 'Theme app block', soon: true },
        { i: 'columns', t: 'iFrame', d: 'Fallback for anywhere' },
        { i: 'zap', t: 'REST API + webhooks', d: 'Headless, versioned' },
      ],
    },
    operators: {
      eyebrow: 'For tour operators', title: 'Upload your offers once. Reach dozens of agencies.',
      sub: 'We connect to your system via XML, JSON, CSV or manual entry. We normalize every offer and make it available to the whole agency network, with source lineage and traceability.',
      adapters: ['XML feed', 'JSON API', 'CSV / SFTP', 'Manual entry'],
      bullets: ['Automatic sync with retry and alerts', 'Preview before activation', 'Every offer traceable to its source'],
      cta: 'Become a supplier',
      stats: [{ v: '100+', l: 'operators in the network' }, { v: '3,200+', l: 'active offers' }, { v: '12', l: 'min avg freshness' }],
    },
    proof: {
      quote: '"We stopped maintaining spreadsheets and dated iframes. We publish once and offers show up across all our sites, in our brand."',
      author: 'Maria Dobreva', role: 'Manager, Slantsev Tur',
      logos: ['Slantsev Tur', 'Nova Travel', 'Balkan Holidays', 'Adriatik', 'Zenit', 'Profi Tours'],
    },
    pricing: {
      eyebrow: 'Pricing', title: 'Transparent pricing that grows with you',
      sub: 'No hidden integration fees. Cancel anytime.', perMonth: '/ mo', popular: 'Popular',
      tiers: [
        { name: 'Start', price: '€0', who: 'For small agencies just starting', cta: 'Start free', popular: false, feats: ['1 active collection', 'Core components', 'Embed via script or iframe', 'Color styling'] },
        { name: 'Pro', price: '€49', who: 'For growing agencies with several sites', cta: 'Book a demo', popular: true, feats: ['Unlimited collections', 'All channels (WP, Wix, API)', 'Full theme system', 'Leads & analytics', 'API key'] },
        { name: 'Enterprise', price: 'Custom', who: 'For platforms and large operators', cta: 'Contact us', popular: false, feats: ['REST API + webhooks', 'SEO publishing', 'Custom connectors', 'Priority support & SLA'] },
      ],
    },
    finalCta: { title: 'Ready to publish everywhere?', sub: 'See Odisea with your offers. 30 minutes, no commitment.', cta: 'Book a demo', cta2: 'Explore the portal' },
    footer: { tagline: 'A platform for distributing and presenting travel products.', rights: '© 2026 Odisea. All rights reserved.', cols: [['Product', ['How it works', 'Channels', 'Pricing', 'API']], ['Company', ['For operators', 'Blog', 'About', 'Careers']], ['Legal', ['Privacy', 'Terms', 'WCAG AA', 'GDPR']]] as [string, string[]][] },
  },
};

type Lang = 'bg' | 'en';
type Theme = 'odisea' | 'paradise' | 'azur';

@Component({
  selector: 'app-landing-page',
  standalone: true,
  imports: [RouterLink, NgTemplateOutlet],
  templateUrl: './landing.page.html',
  styleUrl: './landing.page.css',
})
export class LandingPage {
  private sanitizer = inject(DomSanitizer);
  private svgCache = new Map<string, SafeHtml>();

  lang = signal<Lang>(this.initialLang());
  theme = signal<Theme>('paradise');
  layout = signal<'grid' | 'carousel'>('grid');

  t = computed(() => COPY[this.lang()]);
  offers = DEMO_OFFERS;
  heroOffers = DEMO_OFFERS.slice(0, 2);

  carousel = viewChild<ElementRef<HTMLElement>>('carousel');

  private initialLang(): Lang {
    try {
      const v = localStorage.getItem('odisea_lang');
      if (v === 'bg' || v === 'en') return v;
    } catch { /* ignore */ }
    return 'bg';
  }

  setLang(l: Lang): void {
    this.lang.set(l);
    try { localStorage.setItem('odisea_lang', l); } catch { /* ignore */ }
    document.documentElement.lang = l;
  }

  themeClass = computed(() =>
    this.theme() === 'paradise' ? 'odc-theme-paradise' : this.theme() === 'azur' ? 'odc-theme-azur' : '');

  scrollCarousel(dir: number): void {
    this.carousel()?.nativeElement.scrollBy({ left: dir * 280, behavior: 'smooth' });
  }

  // Lookups used by the offer card template.
  country(code: string): string { return COUNTRY[code]?.bg ?? code; }
  flag(code: string): string { return COUNTRY[code]?.flag ?? ''; }
  cat(id: string): string { return CAT[id] ?? id; }
  board(id: string): string { return BOARD[id] ?? id; }

  svg(name: string, size = 18): SafeHtml {
    const key = `${name}:${size}`;
    let v = this.svgCache.get(key);
    if (!v) {
      const markup = `<svg width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">${ICON_PATHS[name] ?? ''}</svg>`;
      v = this.sanitizer.bypassSecurityTrustHtml(markup);
      this.svgCache.set(key, v);
    }
    return v;
  }
}
