import {
  Component, OnInit, inject, signal, computed, effect,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  ApiService, CollectionDto, OfferDto, ThemeDto,
  ExperienceConfig, CreateCollectionRequest, CreatePublicationRequest,
} from './api.service';
import { EmbedModalComponent } from './embed-modal';

// TODO: replace with real agencyId from auth context (wired in feat/7-tenant-auth)
const CURRENT_AGENCY_ID = '00000000-0000-0000-0000-000000000001';

// ── Static lookup tables (match seeder enum strings) ──────────────────────────

const COUNTRIES = [
  { code: 'GR', bg: 'Гърция',     flag: '🇬🇷' },
  { code: 'TR', bg: 'Турция',     flag: '🇹🇷' },
  { code: 'EG', bg: 'Египет',     flag: '🇪🇬' },
  { code: 'BG', bg: 'България',   flag: '🇧🇬' },
];

const BOARDS = [
  { id: 'AllInclusive',    bg: 'All inclusive' },
  { id: 'FullBoard',       bg: 'Пълен пансион' },
  { id: 'HalfBoard',       bg: 'Полупансион' },
  { id: 'BedAndBreakfast', bg: 'Закуска' },
  { id: 'RoomOnly',        bg: 'Без изхранване' },
];

const TRANSPORTS = [
  { v: 'any',   label: 'Всеки' },
  { v: 'Plane', label: '✈ Самолет' },
  { v: 'Bus',   label: '🚌 Автобус' },
];

const MONTHS = ['Май', 'Юни', 'Юли', 'Август', 'Септември', 'Октомври'];

const PREVIEW_THEMES = [
  { v: 'odisea',   label: 'Odisea',   accent: '#1a5a61', bg: '#f7f9f8', radius: 12 },
  { v: 'paradise', label: 'Paradise', accent: '#C85727', bg: '#f6f1ea', radius: 20 },
  { v: 'azur',     label: 'Azur',     accent: '#1f4ed8', bg: '#f7f9f8', radius: 4 },
];

const EXP_TYPES = [
  { v: 'grid',     label: 'Мрежа',             desc: 'Класическа решетка от карти' },
  { v: 'carousel', label: 'Карусел',            desc: 'Хоризонтален скрол' },
  { v: 'search',   label: 'Търсене + филтри',   desc: 'Странична лента с филтри' },
  { v: 'featured', label: 'Витрина',            desc: 'Голям банер + подбрани' },
];

const CARD_STYLES = [
  { v: 'default',   label: 'Класическа' },
  { v: 'compact',   label: 'Компактна' },
  { v: 'editorial', label: 'Editorial' },
];

const CHANNELS = [
  { id: 'wc',      n: 'Web компонент',      tag: 'Препоръчано', d: '<script> + <odisea-collection>' },
  { id: 'wp',      n: 'WordPress',           tag: 'Приоритет',   d: 'Gutenberg блок + shortcode' },
  { id: 'iframe',  n: 'iFrame fallback',     tag: '',            d: 'За среди без скриптове' },
  { id: 'api',     n: 'REST API + webhooks', tag: '',            d: 'Headless · versioned' },
  { id: 'shopify', n: 'Shopify',             tag: '',            d: 'Theme app block', soon: true },
  { id: 'seo',     n: 'SEO публикуване',     tag: '',            d: 'Native страници · structured data', soon: true },
];

const STEPS = [
  { id: 'collection', label: 'Колекция',    sub: 'Кои оферти' },
  { id: 'experience', label: 'Experience',  sub: 'Кои компоненти' },
  { id: 'theme',      label: 'Тема',        sub: 'Какъв стил' },
  { id: 'publish',    label: 'Публикуване', sub: 'Къде' },
] as const;

type StepId = typeof STEPS[number]['id'];

interface BuilderRules {
  name: string;
  countries: string[];
  boards: string[];
  transport: string;
  priceMax: number;
  months: string[];
  sort: string;
  limit: number;
  autoUpdate: boolean;
}

const DEFAULT_RULES: BuilderRules = {
  name: 'Нова колекция',
  countries: [],
  boards: [],
  transport: 'any',
  priceMax: 1500,
  months: [],
  sort: 'pop',
  limit: 6,
  autoUpdate: true,
};

const DEFAULT_EXP: ExperienceConfig = {
  type: 'grid',
  columns: 3,
  cardStyle: 'default',
  showPrice: true,
  inquiry: true,
  openNewTab: false,
};

function slugify(name: string): string {
  return name.toLowerCase().replace(/[^a-z0-9а-яА-Я]+/g, '-').replace(/^-|-$/g, '') || 'collection';
}

function filterOffers(offers: OfferDto[], rules: BuilderRules): OfferDto[] {
  let out = offers.slice();
  if (rules.countries.length > 0) out = out.filter(o => rules.countries.includes(o.country));
  if (rules.boards.length > 0)    out = out.filter(o => rules.boards.includes(o.boardBasis));
  if (rules.transport !== 'any')  out = out.filter(o => o.transport === rules.transport);
  out = out.filter(o => o.price <= rules.priceMax);
  if (rules.sort === 'price')  out = out.sort((a, b) => a.price - b.price);
  if (rules.sort === 'priceD') out = out.sort((a, b) => b.price - a.price);
  return rules.limit > 0 ? out.slice(0, rules.limit) : out;
}

@Component({
  selector: 'app-builder-page',
  standalone: true,
  imports: [CommonModule, FormsModule, EmbedModalComponent],
  template: `
<!-- ── Top bar ── -->
<header class="bld-bar">
  <div class="bar-left">
    <button class="btn-ghost-sm" (click)="router.navigate(['/collections'])">← Колекции</button>
    <div class="bar-div"></div>
    <div class="pub-id">
      <div class="pub-icon">≡</div>
      <div>
        <div class="pub-name">{{ rules().name }}</div>
        <div class="pub-meta">
          <span class="eyebrow">Publication</span>
          @if (rules().autoUpdate) { <span class="auto-badge">⚡ Авто</span> }
        </div>
      </div>
    </div>
  </div>
  <div class="bar-right">
    @if (error()) {
      <span class="inline-err">{{ error() }}</span>
    }
    <button class="btn-secondary-sm" [disabled]="!canGetEmbed()" (click)="embedOpen.set(true)">
      &lt;/&gt; Вземи embed код
    </button>
    <button class="btn-primary-sm" [disabled]="publishing()" (click)="onNextOrPublish()">
      {{ step() === 'publish' ? (publishing() ? 'Публикуване…' : 'Публикувай') : 'Напред →' }}
    </button>
  </div>
</header>

<!-- ── Stepper ── -->
<nav class="bld-stepper">
  @for (s of steps; track s.id; let i = $index) {
    <button class="step-btn" [class.active]="step() === s.id" [class.done]="stepIdx() > i"
      (click)="goStep(s.id)">
      <span class="step-num">{{ stepIdx() > i ? '✓' : (i + 1) }}</span>
      <span class="step-labels">
        <span class="step-label">{{ s.label }}</span>
        <span class="step-sub">{{ s.sub }}</span>
      </span>
    </button>
    @if (i < steps.length - 1) { <span class="step-sep">›</span> }
  }
</nav>

<!-- ── Body ── -->
<div class="bld-body">

  <!-- ── Left panel ── -->
  <div class="bld-panel">
    <div class="step-eyebrow">Стъпка {{ stepIdx() + 1 }} · {{ steps[stepIdx()].label }}</div>

    <!-- STEP 1: Collection -->
    @if (step() === 'collection') {
      <!-- Collection mode tabs -->
      <div class="tab-row">
        <button class="tab-btn" [class.tab-active]="colMode() === 'pick'"
          (click)="colMode.set('pick')">Изберете колекция</button>
        <button class="tab-btn" [class.tab-active]="colMode() === 'rules'"
          (click)="colMode.set('rules')">Дефинирай правила</button>
      </div>

      @if (colMode() === 'pick') {
        <!-- Collection picker -->
        @if (loadingData()) {
          <p class="muted">Зареждане…</p>
        } @else if (collections().length === 0) {
          <p class="muted">Няма колекции. Използвайте „Дефинирай правила".</p>
        } @else {
          <div class="col-list">
            @for (c of collections(); track c.id) {
              <button class="col-item" [class.col-selected]="selectedColId() === c.id"
                (click)="pickCollection(c.id)">
                <div class="col-item-icon">≡</div>
                <div class="col-item-info">
                  <div class="col-item-name">{{ c.name }}</div>
                  <div class="col-item-slug">{{ c.slug }} · {{ c.status }}</div>
                </div>
                @if (selectedColId() === c.id) { <span class="col-check">✓</span> }
              </button>
            }
          </div>
        }
      } @else {
        <!-- Rules panel -->
        <div class="field-row">
          <label class="field-label">Име на колекцията</label>
          <input class="text-input" [value]="rules().name"
            (input)="setRule('name', getVal($event))" />
        </div>

        <div class="group">
          <div class="group-hd"><span class="group-title">Дестинации</span>
            <span class="group-hint">Празно = всички</span></div>
          <div class="chips">
            @for (c of COUNTRIES; track c.code) {
              <button class="chip" [class.chip-on]="rules().countries.includes(c.code)"
                (click)="toggleArr('countries', c.code)">
                {{ c.flag }} {{ c.bg }}
              </button>
            }
          </div>
        </div>

        <div class="group">
          <div class="group-hd"><span class="group-title">Изхранване</span></div>
          <div class="chips">
            @for (b of BOARDS; track b.id) {
              <button class="chip" [class.chip-on]="rules().boards.includes(b.id)"
                (click)="toggleArr('boards', b.id)">
                {{ b.bg }}
              </button>
            }
          </div>
        </div>

        <div class="group">
          <div class="group-hd">
            <span class="group-title">Бюджет на човек</span>
            <span class="price-badge">до €{{ rules().priceMax }}</span>
          </div>
          <input type="range" class="slider" min="300" max="3500" step="50"
            [value]="rules().priceMax"
            (input)="setRule('priceMax', +getVal($event))"
            [style.background]="sliderBg()" />
          <div class="slider-labels"><span>€300</span><span>€3500</span></div>
        </div>

        <div class="group">
          <div class="group-hd"><span class="group-title">Транспорт</span></div>
          <div class="segmented">
            @for (t of TRANSPORTS; track t.v) {
              <button class="seg-btn" [class.seg-on]="rules().transport === t.v"
                (click)="setRule('transport', t.v)">{{ t.label }}</button>
            }
          </div>
        </div>

        <div class="group">
          <div class="group-hd"><span class="group-title">Период</span>
            <span class="group-hint">Празно = целогодишно</span></div>
          <div class="chips">
            @for (m of MONTHS; track m) {
              <button class="chip" [class.chip-on]="rules().months.includes(m)"
                (click)="toggleArr('months', m)">{{ m }}</button>
            }
          </div>
        </div>

        <div class="group">
          <div class="group-hd"><span class="group-title">Подреждане и брой</span></div>
          <div class="select-row">
            <select class="sel" [value]="rules().sort"
              (change)="setRule('sort', getVal($event))">
              <option value="pop">Препоръчани</option>
              <option value="price">Цена ↑</option>
              <option value="priceD">Цена ↓</option>
            </select>
            <select class="sel" [value]="rules().limit"
              (change)="setRule('limit', +getVal($event))">
              <option [value]="3">3 бр.</option>
              <option [value]="6">6 бр.</option>
              <option [value]="9">9 бр.</option>
              <option [value]="12">12 бр.</option>
            </select>
          </div>
        </div>

        <div class="group">
          <div class="toggle-row">
            <div>
              <div class="tgl-label">Живо обновяване</div>
              <div class="tgl-hint">Новите оферти, отговарящи на правилата, се появяват автоматично</div>
            </div>
            <button class="toggle" [class.tgl-on]="rules().autoUpdate"
              (click)="setRule('autoUpdate', !rules().autoUpdate)">
              <span class="tgl-thumb"></span>
            </button>
          </div>
          @if (rules().autoUpdate) {
            <div class="live-info">
              ⚡ <b>{{ filteredOffers().length }}</b> от {{ allOffers().length }} оферти отговарят
            </div>
          }
        </div>
      }
    }

    <!-- STEP 2: Experience -->
    @if (step() === 'experience') {
      <div class="group">
        <div class="group-hd"><span class="group-title">Тип компонент</span>
          <span class="group-hint">Кои od- компоненти и как се подреждат</span></div>
        <div class="type-grid">
          @for (t of EXP_TYPES; track t.v) {
            <button class="type-card" [class.type-on]="exp().type === t.v"
              (click)="setExp('type', t.v)">
              <div class="type-name">{{ t.label }}</div>
              <div class="type-desc">{{ t.desc }}</div>
            </button>
          }
        </div>
      </div>

      @if (exp().type !== 'carousel') {
        <div class="group">
          <div class="group-hd"><span class="group-title">Колони</span></div>
          <div class="segmented full-seg">
            @for (n of [2, 3, 4]; track n) {
              <button class="seg-btn" [class.seg-on]="exp().columns === n"
                (click)="setExp('columns', n)">{{ n }}</button>
            }
          </div>
        </div>
      }

      <div class="group">
        <div class="group-hd"><span class="group-title">Стил на картата</span></div>
        <div class="segmented full-seg">
          @for (cs of CARD_STYLES; track cs.v) {
            <button class="seg-btn" [class.seg-on]="exp().cardStyle === cs.v"
              (click)="setExp('cardStyle', cs.v)">{{ cs.label }}</button>
          }
        </div>
      </div>

      <div class="group">
        <div class="group-hd"><span class="group-title">Поведение</span></div>
        <div class="toggle-list">
          <div class="toggle-row">
            <div>
              <div class="tgl-label">Показвай цени</div>
              <div class="tgl-hint">Скрий за „запитай за цена" модел</div>
            </div>
            <button class="toggle" [class.tgl-on]="exp().showPrice"
              (click)="setExp('showPrice', !exp().showPrice)">
              <span class="tgl-thumb"></span>
            </button>
          </div>
          <div class="toggle-row">
            <div>
              <div class="tgl-label">Бутон за запитване</div>
              <div class="tgl-hint">od-booking-inquiry под офертите</div>
            </div>
            <button class="toggle" [class.tgl-on]="exp().inquiry"
              (click)="setExp('inquiry', !exp().inquiry)">
              <span class="tgl-thumb"></span>
            </button>
          </div>
          <div class="toggle-row">
            <div><div class="tgl-label">Отваряй в нов таб</div></div>
            <button class="toggle" [class.tgl-on]="exp().openNewTab"
              (click)="setExp('openNewTab', !exp().openNewTab)">
              <span class="tgl-thumb"></span>
            </button>
          </div>
        </div>
      </div>
    }

    <!-- STEP 3: Theme -->
    @if (step() === 'theme') {
      <div class="group">
        <div class="group-hd">
          <span class="group-title">Дизайн тема</span>
          <span class="group-hint">Цялостен набор от токени — не само цветове</span>
        </div>
        @if (loadingData()) {
          <p class="muted">Зареждане…</p>
        } @else if (themes().length === 0) {
          <div class="no-themes">
            <p>Нямате публикувани теми.</p>
            <button class="btn-link" (click)="router.navigate(['/themes', 'new'])">
              Създай тема →
            </button>
          </div>
        } @else {
          <div class="theme-list">
            @for (t of themes(); track t.id) {
              <button class="theme-item" [class.theme-on]="selectedThemeId() === t.id"
                (click)="selectedThemeId.set(t.id)">
                <span class="theme-swatch"
                  [style.background]="t.tokens.foundation['accent'] || '#1a5a61'"></span>
                <div class="theme-info">
                  <div class="theme-name">{{ t.name }}</div>
                  <div class="theme-meta">{{ t.tokens.foundation['fontBody'] || 'Onest' }} ·
                    {{ t.tokens.foundation['radius'] || '8' }}px radius</div>
                </div>
                @if (selectedThemeId() === t.id) { <span class="theme-check">✓</span> }
              </button>
            }
          </div>
        }
      </div>

      @if (selectedThemeId()) {
        <div class="group">
          <div class="group-hd"><span class="group-title">Токени на темата</span></div>
          @if (selectedTheme()) {
            <div class="token-rows">
              <div class="token-row">
                <span class="token-key">Акцент</span>
                <span class="token-val">{{ selectedTheme()!.tokens.foundation['accent'] }}</span>
              </div>
              <div class="token-row">
                <span class="token-key">Шрифт</span>
                <span class="token-val">{{ selectedTheme()!.tokens.foundation['fontBody'] }}</span>
              </div>
              <div class="token-row">
                <span class="token-key">Радиус</span>
                <span class="token-val">{{ selectedTheme()!.tokens.foundation['radius'] }}px</span>
              </div>
            </div>
          }
          <button class="btn-link" (click)="router.navigate(['/themes', selectedThemeId()])">
            ⚙ Редактирай в Теми →
          </button>
        </div>
      }
    }

    <!-- STEP 4: Publish -->
    @if (step() === 'publish') {
      <!-- Status card -->
      <div class="status-card">
        <div class="status-icon" [class.status-pub]="!isDraft()">
          {{ !isDraft() ? '✓' : '👁' }}
        </div>
        <div class="status-info">
          <div class="status-label">{{ !isDraft() ? 'Публикувана' : 'Чернова' }}</div>
          <div class="status-sub">Publication = Колекция + Experience + Тема</div>
        </div>
        <button class="toggle" [class.tgl-on]="!isDraft()"
          (click)="isDraft.set(!isDraft())">
          <span class="tgl-thumb"></span>
        </button>
      </div>

      <!-- Allowed domains -->
      <div class="group">
        <div class="group-hd">
          <span class="group-title">Разрешени домейни</span>
          <span class="group-hint">Празно = всички домейни</span>
        </div>
        <div class="domain-input-row">
          <input class="text-input domain-in" placeholder="example.com"
            [value]="domainInput()"
            (input)="domainInput.set(getVal($event))"
            (keydown.enter)="addDomain()" />
          <button class="btn-add" (click)="addDomain()">+</button>
        </div>
        @if (allowedDomains().length > 0) {
          <div class="domain-chips">
            @for (d of allowedDomains(); track d) {
              <span class="domain-chip">{{ d }}
                <button class="chip-rm" (click)="removeDomain(d)">✕</button>
              </span>
            }
          </div>
        }
      </div>

      <!-- Summary -->
      <div class="group">
        <div class="group-hd"><span class="group-title">Състав на публикацията</span></div>
        <div class="summary-rows">
          <div class="summary-row">
            <span class="sum-icon teal-icon">≡</span>
            <div>
              <div class="sum-key">Колекция</div>
              <div class="sum-val">{{ publishSummaryCollection() }}</div>
            </div>
          </div>
          <div class="summary-row">
            <span class="sum-icon teal-icon">▦</span>
            <div>
              <div class="sum-key">Experience</div>
              <div class="sum-val">{{ exp().type }} · {{ exp().columns }} колони · {{ exp().cardStyle }}</div>
            </div>
          </div>
          <div class="summary-row">
            <span class="sum-icon teal-icon">⚙</span>
            <div>
              <div class="sum-key">Тема</div>
              <div class="sum-val">{{ selectedTheme()?.name ?? 'Без тема (системна)' }}</div>
            </div>
          </div>
        </div>
      </div>

      <!-- Channels -->
      <div class="group">
        <div class="group-hd">
          <span class="group-title">Канали за публикуване</span>
          <span class="group-hint">Един contract, различни adapters</span>
        </div>
        <div class="channels">
          @for (ch of CHANNELS; track ch.id) {
            <div class="channel-row" [class.channel-soon]="ch.soon">
              <div class="ch-icon">{{ chIcon(ch.id) }}</div>
              <div class="ch-info">
                <div class="ch-name">{{ ch.n }}
                  @if (ch.tag) { <span class="ch-tag">{{ ch.tag }}</span> }
                  @if (ch.soon) { <span class="ch-soon">Скоро</span> }
                </div>
                <div class="ch-desc">{{ ch.d }}</div>
              </div>
              @if (!ch.soon) {
                <button class="btn-code-sm" (click)="embedOpen.set(true)">Код</button>
              } @else {
                <button class="btn-ghost-sm" disabled>Заяви</button>
              }
            </div>
          }
        </div>
      </div>
    }
  </div>

  <!-- ── Right: live preview ── -->
  <div class="bld-preview">
    <!-- Preview controls -->
    <div class="preview-ctrl">
      <div class="preview-info">
        <span class="badge-teal">👁 Жив преглед</span>
        <span class="preview-count">
          <b>{{ previewOffers().length }}</b> оферти · {{ exp().type }}
        </span>
      </div>
      <div class="preview-actions">
        <!-- Theme switcher -->
        <div class="seg-sm">
          @for (pt of PREVIEW_THEMES; track pt.v) {
            <button class="seg-sm-btn" [class.seg-sm-on]="previewTheme() === pt.v"
              (click)="previewTheme.set(pt.v)">{{ pt.label }}</button>
          }
        </div>
        <!-- Device switcher -->
        <div class="device-sw">
          <button class="dev-btn" [class.dev-on]="device() === 'desktop'"
            (click)="device.set('desktop')" title="Desktop">▣</button>
          <button class="dev-btn" [class.dev-on]="device() === 'mobile'"
            (click)="device.set('mobile')" title="Mobile">▨</button>
        </div>
      </div>
    </div>

    <!-- Browser frame wrapper -->
    <div class="frame-outer">
      <div class="frame-sizer" [class.frame-mobile]="device() === 'mobile'">
        <div class="browser-frame">
          <!-- Chrome bar -->
          <div class="chrome-bar">
            <div class="traffic-lights">
              <span style="background:#f0685f"></span>
              <span style="background:#f6bd3b"></span>
              <span style="background:#61c454"></span>
            </div>
            <div class="url-bar">🔗 agency.bg/offers</div>
          </div>
          <!-- Content -->
          <div class="frame-content" [ngStyle]="previewVars()">
            @if (previewOffers().length === 0) {
              <div class="empty-preview">
                <div class="empty-icon">📭</div>
                <div>Няма оферти за текущите правила</div>
              </div>
            } @else {
              @if (exp().type === 'carousel') {
                <div class="prev-section">
                  <div class="prev-title">{{ rules().name }}</div>
                  <div class="prev-carousel">
                    @for (o of previewOffers(); track o.id) {
                      <div class="prev-card carousel-card" [ngStyle]="cardVars()">
                        <div class="prev-img" [style.backgroundImage]="safeImg(o.imageUrl)"></div>
                        <div class="prev-body">
                          <div class="prev-card-title">{{ o.title }}</div>
                          <div class="prev-card-sub">{{ o.country }} · {{ o.durationNights }} нощи</div>
                          @if (exp().showPrice) { <div class="prev-price">от {{ o.price }} {{ o.currency }}</div> }
                          <button class="prev-cta" [ngStyle]="ctaVars()">Виж детайли</button>
                        </div>
                      </div>
                    }
                  </div>
                </div>
              } @else if (exp().type === 'featured') {
                @if (previewOffers().length > 0) {
                  <div class="prev-hero" [style.backgroundImage]="safeImg(previewOffers()[0].imageUrl)">
                    <div class="hero-overlay">
                      <div class="hero-title">{{ previewOffers()[0].title }}</div>
                      @if (exp().showPrice) {
                        <div class="hero-price">от {{ previewOffers()[0].price }} {{ previewOffers()[0].currency }}</div>
                      }
                      <button class="hero-cta" [ngStyle]="ctaVars()">Виж детайли</button>
                    </div>
                  </div>
                }
                <div class="prev-section">
                  <div class="prev-title">Още от колекцията</div>
                  <div class="prev-grid" [ngStyle]="gridVars()">
                    @for (o of previewOffers().slice(1); track o.id) {
                      <div class="prev-card" [ngStyle]="cardVars()">
                        <div class="prev-img" [style.backgroundImage]="safeImg(o.imageUrl)"></div>
                        <div class="prev-body">
                          <div class="prev-card-title">{{ o.title }}</div>
                          <div class="prev-card-sub">{{ o.country }} · {{ o.durationNights }} нощи</div>
                          @if (exp().showPrice) { <div class="prev-price">от {{ o.price }} {{ o.currency }}</div> }
                          <button class="prev-cta" [ngStyle]="ctaVars()">Виж детайли</button>
                        </div>
                      </div>
                    }
                  </div>
                </div>
              } @else if (exp().type === 'search') {
                <div class="prev-search-layout" [class.prev-stacked]="device() === 'mobile'">
                  @if (device() !== 'mobile') {
                    <div class="prev-filters">
                      <div class="filter-head">Филтри</div>
                      <div class="filter-item">Дестинация</div>
                      <div class="filter-item">Изхранване</div>
                      <div class="filter-item">Транспорт</div>
                      <div class="filter-item">Цена</div>
                    </div>
                  }
                  <div class="prev-grid-wrap">
                    <div class="prev-title">{{ rules().name }}
                      <span class="count-badge">({{ previewOffers().length }})</span>
                    </div>
                    <div class="prev-grid" [ngStyle]="searchGridVars()">
                      @for (o of previewOffers(); track o.id) {
                        <div class="prev-card" [ngStyle]="cardVars()">
                          <div class="prev-img" [style.backgroundImage]="safeImg(o.imageUrl)"></div>
                          <div class="prev-body">
                            <div class="prev-card-title">{{ o.title }}</div>
                            <div class="prev-card-sub">{{ o.country }} · {{ o.durationNights }} нощи</div>
                            @if (exp().showPrice) { <div class="prev-price">от {{ o.price }} {{ o.currency }}</div> }
                            <button class="prev-cta" [ngStyle]="ctaVars()">Виж детайли</button>
                          </div>
                        </div>
                      }
                    </div>
                  </div>
                </div>
              } @else {
                <!-- Default: grid -->
                <div class="prev-section">
                  <div class="prev-title">{{ rules().name }}</div>
                  <div class="prev-grid" [ngStyle]="gridVars()">
                    @for (o of previewOffers(); track o.id) {
                      <div class="prev-card" [ngStyle]="cardVars()">
                        <div class="prev-img" [style.backgroundImage]="safeImg(o.imageUrl)"></div>
                        <div class="prev-body">
                          <div class="prev-card-title">{{ o.title }}</div>
                          <div class="prev-card-sub">{{ o.country }} · {{ o.durationNights }} нощи</div>
                          @if (exp().showPrice) { <div class="prev-price">от {{ o.price }} {{ o.currency }}</div> }
                          <button class="prev-cta" [ngStyle]="ctaVars()">Виж детайли</button>
                        </div>
                      </div>
                    }
                  </div>
                </div>
              }

              @if (exp().inquiry) {
                <div class="prev-inquiry">
                  <div class="inquiry-box">
                    <div class="inquiry-title">Запитване за {{ rules().name }}</div>
                    <div class="inquiry-fields">
                      <div class="inquiry-field">Вашето име</div>
                      <div class="inquiry-field">Email</div>
                    </div>
                    <button class="inquiry-btn" [ngStyle]="ctaVars()">Изпрати запитване</button>
                  </div>
                </div>
              }
            }
          </div>
        </div>
      </div>
    </div>
  </div>

</div>

<!-- ── Embed modal ── -->
@if (embedOpen()) {
  <app-embed-modal
    [publicationName]="rules().name"
    [publicationKey]="createdKey() ?? 'demo_key'"
    (close)="embedOpen.set(false)" />
}
  `,
  styles: [`
    :host {
      display: flex; flex-direction: column;
      height: calc(100vh - 52px);
      margin: -24px -28px;
      overflow: hidden;
      font-family: var(--od-font, system-ui);
      background: var(--od-50, #f7f9f8);
    }

    /* ── Top bar ── */
    .bld-bar {
      display: flex; align-items: center; justify-content: space-between; gap: 14px;
      padding: 12px 24px; border-bottom: 1px solid var(--od-border, #e1e6e4);
      background: var(--od-surface, #fff); flex: none; flex-wrap: wrap;
    }
    .bar-left  { display: flex; align-items: center; gap: 10px; min-width: 0; }
    .bar-right { display: flex; align-items: center; gap: 9px; flex-wrap: wrap; }
    .bar-div { width: 1px; height: 20px; background: var(--od-200, #e1e6e4); }
    .pub-id { display: flex; align-items: center; gap: 9px; min-width: 0; }
    .pub-icon {
      width: 30px; height: 30px; border-radius: 7px; flex: none;
      background: var(--od-gold-50, #faf4e7); color: var(--od-gold-700, #9a7434);
      display: grid; place-items: center; font-size: 16px;
    }
    .pub-name { font-size: 14px; font-weight: 700; color: var(--od-ink, #0e1618); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 220px; }
    .pub-meta { display: flex; align-items: center; gap: 7px; font-size: 11px; color: var(--od-500, #6c7976); }
    .eyebrow { font-size: 10px; font-weight: 700; letter-spacing: .07em; text-transform: uppercase; }
    .auto-badge { color: var(--od-teal-600, #1a5a61); }
    .inline-err { font-size: 12px; color: var(--od-danger, #b3261e); max-width: 220px; }

    .btn-ghost-sm {
      display: inline-flex; align-items: center; gap: 5px; height: 32px; padding: 0 10px;
      border: 0; border-radius: var(--od-r-md, 8px); background: transparent;
      color: var(--od-600, #525f5c); font-family: var(--od-font, system-ui); font-size: 13px;
      font-weight: 500; cursor: pointer; transition: background var(--od-fast, 120ms ease);
    }
    .btn-ghost-sm:hover { background: var(--od-100, #f1f4f3); }
    .btn-ghost-sm:disabled { opacity: .45; cursor: not-allowed; }
    .btn-secondary-sm {
      height: 32px; padding: 0 12px; border: 1px solid var(--od-200, #e1e6e4);
      border-radius: var(--od-r-md, 8px); background: var(--od-surface, #fff);
      color: var(--od-ink, #0e1618); font-family: var(--od-font, system-ui);
      font-size: 13px; font-weight: 600; cursor: pointer;
      transition: background var(--od-fast, 120ms ease);
    }
    .btn-secondary-sm:hover { background: var(--od-50, #f7f9f8); }
    .btn-secondary-sm:disabled { opacity: .45; cursor: not-allowed; }
    .btn-primary-sm {
      height: 32px; padding: 0 14px; border: none; border-radius: var(--od-r-md, 8px);
      background: var(--od-teal-600, #1a5a61); color: #fff;
      font-family: var(--od-font, system-ui); font-size: 13px; font-weight: 600;
      cursor: pointer; transition: background var(--od-fast, 120ms ease);
    }
    .btn-primary-sm:hover { background: var(--od-teal-700, #14474e); }
    .btn-primary-sm:disabled { opacity: .55; cursor: not-allowed; }

    /* ── Stepper ── */
    .bld-stepper {
      display: flex; align-items: center; gap: 4px; padding: 0 24px;
      height: 52px; border-bottom: 1px solid var(--od-border, #e1e6e4);
      background: var(--od-surface, #fff); overflow-x: auto; flex: none;
    }
    .step-btn {
      display: flex; align-items: center; gap: 8px; padding: 6px 10px;
      border: 0; border-radius: var(--od-r-md, 8px); cursor: pointer;
      background: transparent; transition: all var(--od-fast, 120ms ease);
    }
    .step-btn.active { background: var(--od-teal-50, #eef6f6); }
    .step-num {
      width: 24px; height: 24px; border-radius: 50%; display: grid; place-items: center;
      font-size: 11.5px; font-weight: 700; flex: none;
      background: var(--od-100, #f1f4f3); color: var(--od-500, #6c7976);
      transition: all var(--od-fast, 120ms ease);
    }
    .step-btn.active .step-num { background: var(--od-teal-600, #1a5a61); color: #fff; }
    .step-btn.done  .step-num { background: var(--od-teal-100, #dcebec); color: var(--od-teal-700, #14474e); }
    .step-labels { text-align: left; }
    .step-label { display: block; font-size: 12.5px; font-weight: 600; color: var(--od-700, #3a4744); }
    .step-btn.active .step-label { color: var(--od-teal-700, #14474e); font-weight: 700; }
    .step-sub { display: block; font-size: 10px; color: var(--od-500, #6c7976); }
    .step-sep { font-size: 14px; color: var(--od-300, #c5cdca); flex: none; }

    /* ── Body ── */
    .bld-body {
      flex: 1; min-height: 0; display: grid;
      grid-template-columns: minmax(360px, 420px) 1fr;
    }

    /* ── Left panel ── */
    .bld-panel {
      border-right: 1px solid var(--od-border, #e1e6e4);
      background: var(--od-surface, #fff); overflow-y: auto;
      padding: 16px 22px 40px;
    }
    .step-eyebrow {
      font-size: 10px; font-weight: 700; letter-spacing: .07em; text-transform: uppercase;
      color: var(--od-500, #6c7976); margin-bottom: 12px;
    }
    .muted { color: var(--od-500, #6c7976); font-size: 13px; }

    /* Collection tabs */
    .tab-row { display: flex; gap: 2px; border-bottom: 1px solid var(--od-border, #e1e6e4); margin-bottom: 14px; }
    .tab-btn {
      padding: 8px 13px; border: 0; background: transparent; cursor: pointer;
      font-family: var(--od-font, system-ui); font-size: 13px; font-weight: 500;
      color: var(--od-600, #525f5c); border-bottom: 2px solid transparent; margin-bottom: -1px;
      transition: all var(--od-fast, 120ms ease);
    }
    .tab-active { color: var(--od-teal-700, #14474e); font-weight: 700; border-bottom-color: var(--od-teal-600, #1a5a61); }

    /* Collection list */
    .col-list { display: flex; flex-direction: column; gap: 7px; }
    .col-item {
      display: flex; align-items: center; gap: 11px; padding: 11px 12px;
      border: 1px solid var(--od-border, #e1e6e4); border-radius: var(--od-r-md, 8px);
      background: var(--od-surface, #fff); cursor: pointer; text-align: left;
      transition: all var(--od-fast, 120ms ease);
    }
    .col-item:hover { border-color: var(--od-teal-400, #3f9098); }
    .col-selected { border-color: var(--od-teal-600, #1a5a61); background: var(--od-teal-50, #eef6f6); }
    .col-item-icon {
      width: 32px; height: 32px; border-radius: 7px; flex: none;
      background: var(--od-100, #f1f4f3); color: var(--od-600, #525f5c);
      display: grid; place-items: center; font-size: 14px;
    }
    .col-selected .col-item-icon { background: var(--od-teal-100, #dcebec); color: var(--od-teal-700, #14474e); }
    .col-item-info { flex: 1; min-width: 0; }
    .col-item-name { font-size: 13px; font-weight: 600; color: var(--od-ink, #0e1618); }
    .col-item-slug { font-size: 11.5px; color: var(--od-500, #6c7976); }
    .col-check { color: var(--od-teal-600, #1a5a61); font-weight: 700; }

    /* Form elements */
    .field-row { margin-bottom: 14px; }
    .field-label { display: block; font-size: 11px; font-weight: 700; letter-spacing: .06em; text-transform: uppercase; color: var(--od-500, #6c7976); margin-bottom: 6px; }
    .text-input {
      width: 100%; height: 38px; padding: 0 11px;
      border: 1px solid var(--od-border, #e1e6e4); border-radius: var(--od-r-md, 8px);
      font-family: var(--od-font, system-ui); font-size: 14px; font-weight: 600;
      color: var(--od-ink, #0e1618); background: var(--od-surface, #fff); outline: none;
      transition: border-color var(--od-fast, 120ms ease);
    }
    .text-input:focus { border-color: var(--od-teal-400, #3f9098); }

    /* Groups */
    .group { padding: 12px 0; border-top: 1px solid var(--od-border-2, #eaeeec); }
    .group-hd { display: flex; align-items: center; justify-content: space-between; margin-bottom: 9px; }
    .group-title { font-size: 12.5px; font-weight: 700; color: var(--od-ink, #0e1618); }
    .group-hint { font-size: 11px; color: var(--od-500, #6c7976); }

    /* Chips */
    .chips { display: flex; flex-wrap: wrap; gap: 6px; }
    .chip {
      display: inline-flex; align-items: center; height: 28px; padding: 0 10px;
      border-radius: var(--od-r-pill, 999px); cursor: pointer;
      font-family: var(--od-font, system-ui); font-size: 12px; font-weight: 500;
      border: 1px solid var(--od-border, #e1e6e4); background: var(--od-surface, #fff);
      color: var(--od-700, #3a4744); transition: all var(--od-fast, 120ms ease);
    }
    .chip-on { border-color: var(--od-teal-600, #1a5a61); background: var(--od-teal-600, #1a5a61); color: #fff; }

    /* Slider */
    .slider {
      width: 100%; height: 6px; border-radius: 999px; appearance: none; cursor: pointer;
      outline: none;
    }
    .slider::-webkit-slider-thumb {
      appearance: none; width: 16px; height: 16px; border-radius: 50%;
      background: var(--od-teal-600, #1a5a61); cursor: pointer;
      box-shadow: 0 1px 3px rgba(0,0,0,.2);
    }
    .slider-labels { display: flex; justify-content: space-between; margin-top: 5px; font-size: 11px; color: var(--od-400, #97a3a0); }
    .price-badge { font-size: 13px; font-weight: 700; color: var(--od-teal-700, #14474e); font-variant-numeric: tabular-nums; }

    /* Segmented */
    .segmented { display: inline-flex; background: var(--od-100, #f1f4f3); border-radius: var(--od-r-md, 8px); padding: 3px; gap: 2px; }
    .full-seg { display: flex; width: 100%; }
    .full-seg .seg-btn { flex: 1; justify-content: center; }
    .seg-btn {
      display: inline-flex; align-items: center; height: 30px; padding: 0 11px;
      border: 0; border-radius: var(--od-r-sm, 6px); cursor: pointer;
      font-family: var(--od-font, system-ui); font-size: 12px; font-weight: 600;
      background: transparent; color: var(--od-600, #525f5c); transition: all var(--od-fast, 120ms ease);
    }
    .seg-on { background: var(--od-surface, #fff); color: var(--od-ink, #0e1618); box-shadow: var(--od-shadow-xs, 0 1px 2px rgba(14,22,24,.05)); }

    /* Select row */
    .select-row { display: flex; gap: 8px; }
    .sel {
      flex: 1; height: 34px; padding: 0 8px;
      border: 1px solid var(--od-border, #e1e6e4); border-radius: var(--od-r-md, 8px);
      font-family: var(--od-font, system-ui); font-size: 13px; color: var(--od-ink, #0e1618);
      background: var(--od-surface, #fff); cursor: pointer;
    }

    /* Toggle */
    .toggle-list { border-top: 1px solid var(--od-border-2, #eaeeec); }
    .toggle-row { display: flex; align-items: center; gap: 12px; padding: 9px 0; }
    .tgl-label { font-size: 13px; font-weight: 600; color: var(--od-ink, #0e1618); }
    .tgl-hint { font-size: 11.5px; color: var(--od-500, #6c7976); margin-top: 1px; }
    .toggle {
      width: 38px; height: 22px; border-radius: 999px; border: 0; cursor: pointer; padding: 2px;
      background: var(--od-300, #c5cdca); transition: background var(--od-base, 180ms ease); flex: none;
    }
    .tgl-on { background: var(--od-teal-600, #1a5a61); }
    .tgl-thumb {
      display: block; width: 18px; height: 18px; border-radius: 50%; background: #fff;
      box-shadow: 0 1px 3px rgba(0,0,0,.2);
      transform: translateX(0); transition: transform var(--od-base, 180ms ease);
    }
    .tgl-on .tgl-thumb { transform: translateX(16px); }
    .live-info {
      display: flex; align-items: center; gap: 8px; padding: 8px 10px; margin-top: 4px;
      background: var(--od-teal-50, #eef6f6); border-radius: var(--od-r-md, 8px);
      font-size: 12px; color: var(--od-teal-700, #14474e);
    }

    /* Experience type grid */
    .type-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
    .type-card {
      padding: 11px 12px; border-radius: var(--od-r-md, 8px); cursor: pointer; text-align: left;
      border: 1px solid var(--od-border, #e1e6e4); background: var(--od-surface, #fff);
      transition: all var(--od-fast, 120ms ease);
    }
    .type-on { border-color: var(--od-teal-600, #1a5a61); background: var(--od-teal-50, #eef6f6); }
    .type-name { font-size: 12.5px; font-weight: 700; color: var(--od-700, #3a4744); margin-bottom: 3px; }
    .type-on .type-name { color: var(--od-teal-700, #14474e); }
    .type-desc { font-size: 11px; color: var(--od-500, #6c7976); }

    /* Theme list */
    .theme-list { display: flex; flex-direction: column; gap: 8px; }
    .theme-item {
      display: flex; align-items: center; gap: 12px; padding: 12px 13px;
      border: 1px solid var(--od-border, #e1e6e4); border-radius: var(--od-r-md, 8px);
      background: var(--od-surface, #fff); cursor: pointer; text-align: left;
      transition: all var(--od-fast, 120ms ease);
    }
    .theme-on { border-color: var(--od-teal-600, #1a5a61); background: var(--od-teal-50, #eef6f6); }
    .theme-swatch { width: 34px; height: 34px; border-radius: 8px; flex: none; box-shadow: var(--od-shadow-xs, 0 1px 2px rgba(14,22,24,.05)); }
    .theme-info { flex: 1; min-width: 0; }
    .theme-name { font-size: 13px; font-weight: 700; color: var(--od-ink, #0e1618); }
    .theme-meta { font-size: 11.5px; color: var(--od-500, #6c7976); }
    .theme-check { color: var(--od-teal-600, #1a5a61); font-weight: 700; }
    .no-themes { font-size: 13px; color: var(--od-500, #6c7976); }
    .token-rows { display: flex; flex-direction: column; gap: 6px; margin-bottom: 10px; }
    .token-row { display: flex; justify-content: space-between; padding: 6px 10px; background: var(--od-50, #f7f9f8); border-radius: 7px; font-size: 12px; }
    .token-key { color: var(--od-500, #6c7976); }
    .token-val { font-family: var(--od-mono, monospace); font-weight: 600; color: var(--od-ink, #0e1618); }
    .btn-link {
      border: 0; background: transparent; color: var(--od-teal-700, #14474e);
      font-family: var(--od-font, system-ui); font-size: 13px; font-weight: 600;
      cursor: pointer; padding: 0; display: inline-flex; align-items: center; gap: 5px;
      transition: color var(--od-fast, 120ms ease);
    }
    .btn-link:hover { color: var(--od-teal-600, #1a5a61); }

    /* Publish step */
    .status-card {
      display: flex; align-items: center; gap: 11px; padding: 13px 14px;
      border: 1px solid var(--od-border, #e1e6e4); border-radius: var(--od-r-lg, 12px);
      background: var(--od-surface, #fff); margin-bottom: 14px;
    }
    .status-icon {
      width: 36px; height: 36px; border-radius: 9px; flex: none;
      background: var(--od-100, #f1f4f3); color: var(--od-500, #6c7976);
      display: grid; place-items: center; font-size: 17px; transition: all var(--od-base, 180ms ease);
    }
    .status-pub { background: var(--od-success-bg, #e7f3ec); color: var(--od-success, #1c7a4a); }
    .status-info { flex: 1; }
    .status-label { font-size: 13px; font-weight: 700; color: var(--od-ink, #0e1618); }
    .status-sub { font-size: 11.5px; color: var(--od-500, #6c7976); }

    .domain-input-row { display: flex; gap: 7px; }
    .domain-in { flex: 1; }
    .btn-add {
      width: 36px; height: 38px; border: 1px solid var(--od-200, #e1e6e4);
      border-radius: var(--od-r-md, 8px); background: var(--od-surface, #fff);
      font-size: 18px; cursor: pointer; display: grid; place-items: center;
      color: var(--od-600, #525f5c); transition: background var(--od-fast, 120ms ease);
    }
    .btn-add:hover { background: var(--od-50, #f7f9f8); }
    .domain-chips { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 7px; }
    .domain-chip {
      display: inline-flex; align-items: center; gap: 5px; height: 26px; padding: 0 9px;
      border-radius: var(--od-r-pill, 999px); background: var(--od-100, #f1f4f3);
      color: var(--od-700, #3a4744); font-size: 12px;
    }
    .chip-rm {
      border: 0; background: transparent; cursor: pointer; color: var(--od-500, #6c7976);
      font-size: 11px; padding: 0; line-height: 1;
    }

    .summary-rows { display: flex; flex-direction: column; gap: 7px; }
    .summary-row { display: flex; align-items: center; gap: 11px; padding: 9px 11px; background: var(--od-50, #f7f9f8); border-radius: var(--od-r-md, 8px); }
    .sum-icon { font-size: 14px; flex: none; }
    .teal-icon { color: var(--od-teal-600, #1a5a61); }
    .sum-key { font-size: 12px; font-weight: 600; color: var(--od-ink, #0e1618); }
    .sum-val { font-size: 11.5px; color: var(--od-500, #6c7976); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

    .channels { display: flex; flex-direction: column; gap: 7px; }
    .channel-row {
      display: flex; align-items: center; gap: 11px; padding: 10px 12px;
      border: 1px solid var(--od-border, #e1e6e4); border-radius: var(--od-r-md, 8px);
      background: var(--od-surface, #fff);
    }
    .channel-soon { opacity: .65; }
    .ch-icon {
      width: 30px; height: 30px; border-radius: 7px; flex: none;
      background: var(--od-teal-50, #eef6f6); color: var(--od-teal-600, #1a5a61);
      display: grid; place-items: center; font-size: 12px; font-weight: 700;
    }
    .ch-info { flex: 1; min-width: 0; }
    .ch-name { font-size: 12.5px; font-weight: 600; color: var(--od-ink, #0e1618); display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
    .ch-tag {
      font-size: 9px; font-weight: 700; text-transform: uppercase; letter-spacing: .04em;
      color: var(--od-gold-700, #9a7434); background: var(--od-gold-50, #faf4e7);
      padding: 2px 5px; border-radius: 4px;
    }
    .ch-soon {
      font-size: 9px; font-weight: 700; text-transform: uppercase;
      color: var(--od-500, #6c7976); background: var(--od-100, #f1f4f3); padding: 2px 5px; border-radius: 4px;
    }
    .ch-desc { font-size: 11.5px; color: var(--od-500, #6c7976); }
    .btn-code-sm {
      height: 28px; padding: 0 10px; border: 1px solid var(--od-200, #e1e6e4);
      border-radius: var(--od-r-sm, 6px); background: var(--od-surface, #fff);
      color: var(--od-700, #3a4744); font-family: var(--od-font, system-ui);
      font-size: 12px; font-weight: 600; cursor: pointer;
      transition: background var(--od-fast, 120ms ease);
    }
    .btn-code-sm:hover { background: var(--od-50, #f7f9f8); }

    /* ── Right: preview ── */
    .bld-preview {
      display: flex; flex-direction: column; min-width: 0;
      padding: 14px 18px 18px; gap: 12px; background: var(--od-50, #f7f9f8);
      overflow: hidden;
    }
    .preview-ctrl {
      display: flex; align-items: center; justify-content: space-between; gap: 12px; flex: none; flex-wrap: wrap;
    }
    .preview-info { display: flex; align-items: center; gap: 9px; }
    .badge-teal {
      display: inline-flex; padding: 3px 9px; border-radius: var(--od-r-pill, 999px);
      background: var(--od-teal-100, #dcebec); color: var(--od-teal-800, #0f3a40);
      font-size: 12px; font-weight: 600;
    }
    .preview-count { font-size: 12.5px; color: var(--od-600, #525f5c); }
    .preview-count b { color: var(--od-ink, #0e1618); }
    .preview-actions { display: flex; align-items: center; gap: 8px; }

    /* Segmented small (theme switcher) */
    .seg-sm { display: inline-flex; background: var(--od-surface, #fff); border: 1px solid var(--od-200, #e1e6e4); border-radius: var(--od-r-md, 8px); padding: 2px; gap: 1px; }
    .seg-sm-btn {
      height: 26px; padding: 0 9px; border: 0; border-radius: 5px; cursor: pointer;
      font-family: var(--od-font, system-ui); font-size: 11.5px; font-weight: 500;
      background: transparent; color: var(--od-600, #525f5c); transition: all var(--od-fast, 120ms ease);
    }
    .seg-sm-on { background: var(--od-teal-600, #1a5a61); color: #fff; font-weight: 600; }
    .device-sw { display: flex; border: 1px solid var(--od-200, #e1e6e4); border-radius: var(--od-r-md, 8px); overflow: hidden; }
    .dev-btn {
      width: 30px; height: 30px; border: 0; cursor: pointer; font-size: 13px;
      background: transparent; color: var(--od-500, #6c7976); transition: all var(--od-fast, 120ms ease);
    }
    .dev-on { background: var(--od-teal-50, #eef6f6); color: var(--od-teal-700, #14474e); }

    /* Frame */
    .frame-outer {
      flex: 1; min-height: 0; display: flex; justify-content: center;
      align-items: flex-start; background: var(--od-100, #f1f4f3);
      border-radius: var(--od-r-lg, 12px); padding: 16px; overflow: auto;
    }
    .frame-sizer { width: 100%; height: 100%; transition: max-width var(--od-base, 180ms ease); }
    .frame-mobile { max-width: 390px; }
    .browser-frame {
      height: 100%; min-height: 400px; border-radius: var(--od-r-lg, 12px); overflow: hidden;
      border: 1px solid var(--od-200, #e1e6e4); box-shadow: var(--od-shadow-md, 0 4px 12px rgba(14,22,24,.08));
      display: flex; flex-direction: column; background: #fff;
    }
    .chrome-bar {
      height: 36px; background: var(--od-100, #f1f4f3); border-bottom: 1px solid var(--od-200, #e1e6e4);
      display: flex; align-items: center; gap: 8px; padding: 0 12px; flex: none;
    }
    .traffic-lights { display: flex; gap: 5px; }
    .traffic-lights span { width: 10px; height: 10px; border-radius: 50%; }
    .url-bar {
      flex: 1; max-width: 300px; margin: 0 auto; height: 22px;
      background: var(--od-surface, #fff); border-radius: 5px;
      display: flex; align-items: center; padding: 0 8px;
      font-size: 11px; color: var(--od-500, #6c7976);
    }
    .frame-content { flex: 1; overflow-y: auto; padding: 20px; }
    .empty-preview {
      display: flex; flex-direction: column; align-items: center; gap: 10px;
      padding: 40px; color: var(--od-400, #97a3a0); font-size: 13px; text-align: center;
    }
    .empty-icon { font-size: 32px; }

    /* Preview components */
    .prev-section { margin-bottom: 20px; }
    .prev-title { font-size: 16px; font-weight: 700; margin-bottom: 14px; }
    .count-badge { font-size: 12px; font-weight: 400; color: var(--od-500, #6c7976); }
    .prev-grid { display: grid; gap: 14px; }
    .prev-card {
      border-radius: 8px; overflow: hidden;
      box-shadow: 0 1px 4px rgba(0,0,0,.08); background: #fff;
    }
    .prev-img { width: 100%; height: 160px; background: var(--od-100, #f1f4f3); background-size: cover; background-position: center; }
    .prev-body { padding: 12px; }
    .prev-card-title { font-size: 13px; font-weight: 700; color: #0e1618; margin-bottom: 3px; line-height: 1.3; }
    .prev-card-sub { font-size: 11.5px; color: #6c7976; margin-bottom: 7px; }
    .prev-price { font-size: 14px; font-weight: 700; margin-bottom: 8px; }
    .prev-cta {
      width: 100%; padding: 7px; border-radius: 7px; border: none;
      color: #fff; font-size: 12.5px; font-weight: 600; cursor: default;
    }
    .prev-carousel { display: flex; gap: 12px; overflow-x: auto; padding-bottom: 8px; }
    .carousel-card { min-width: 220px; max-width: 220px; flex: none; }
    .prev-hero {
      height: 200px; border-radius: 10px; overflow: hidden; position: relative;
      background-size: cover; background-position: center; margin-bottom: 16px;
    }
    .hero-overlay {
      position: absolute; inset: 0; background: linear-gradient(to top, rgba(0,0,0,.65) 50%, transparent);
      padding: 16px; display: flex; flex-direction: column; justify-content: flex-end;
    }
    .hero-title { font-size: 16px; font-weight: 700; color: #fff; margin-bottom: 5px; }
    .hero-price { font-size: 14px; color: rgba(255,255,255,.85); margin-bottom: 10px; }
    .hero-cta {
      align-self: flex-start; padding: 7px 16px; border-radius: 7px; border: none;
      color: #fff; font-size: 13px; font-weight: 600; cursor: default;
    }
    .prev-search-layout { display: flex; gap: 14px; }
    .prev-stacked { flex-direction: column; }
    .prev-filters {
      width: 150px; flex: none; background: var(--od-50, #f7f9f8);
      border: 1px solid var(--od-200, #e1e6e4); border-radius: 8px; padding: 12px;
    }
    .filter-head { font-size: 12px; font-weight: 700; margin-bottom: 10px; color: var(--od-700, #3a4744); }
    .filter-item { font-size: 12px; color: var(--od-600, #525f5c); padding: 6px 0; border-bottom: 1px solid var(--od-150, #eaeeec); }
    .prev-grid-wrap { flex: 1; min-width: 0; }
    .prev-inquiry { margin-top: 18px; }
    .inquiry-box { padding: 16px; border: 1px solid var(--od-200, #e1e6e4); border-radius: 10px; background: var(--od-50, #f7f9f8); }
    .inquiry-title { font-size: 14px; font-weight: 700; margin-bottom: 12px; color: var(--od-ink, #0e1618); }
    .inquiry-fields { display: flex; flex-direction: column; gap: 8px; margin-bottom: 12px; }
    .inquiry-field {
      height: 34px; border: 1px solid var(--od-200, #e1e6e4); border-radius: 7px;
      background: #fff; display: flex; align-items: center; padding: 0 10px;
      font-size: 12px; color: var(--od-400, #97a3a0);
    }
    .inquiry-btn { width: 100%; padding: 8px; border-radius: 7px; border: none; color: #fff; font-size: 13px; font-weight: 600; cursor: default; }
  `],
})
export class BuilderPage implements OnInit {
  router = inject(Router);
  private api = inject(ApiService);

  readonly steps = STEPS;
  readonly COUNTRIES = COUNTRIES;
  readonly BOARDS = BOARDS;
  readonly TRANSPORTS = TRANSPORTS;
  readonly MONTHS = MONTHS;
  readonly EXP_TYPES = EXP_TYPES;
  readonly CARD_STYLES = CARD_STYLES;
  readonly CHANNELS = CHANNELS;
  readonly PREVIEW_THEMES = PREVIEW_THEMES;

  // Navigation
  step = signal<StepId>('collection');
  device = signal<'desktop' | 'mobile'>('desktop');
  previewTheme = signal<string>('odisea');

  // Data
  collections = signal<CollectionDto[]>([]);
  allOffers = signal<OfferDto[]>([]);
  themes = signal<ThemeDto[]>([]);
  loadingData = signal(true);

  // Collection step
  colMode = signal<'pick' | 'rules'>('pick');
  selectedColId = signal<string | null>(null);
  rules = signal<BuilderRules>({ ...DEFAULT_RULES });
  collectionOffers = signal<OfferDto[]>([]);

  // Experience step
  exp = signal<ExperienceConfig>({ ...DEFAULT_EXP });

  // Theme step
  selectedThemeId = signal<string | null>(null);

  // Publish step
  isDraft = signal(true);
  allowedDomains = signal<string[]>([]);
  domainInput = signal('');

  // Publish action
  publishing = signal(false);
  error = signal<string | null>(null);
  createdKey = signal<string | null>(null);
  embedOpen = signal(false);

  // Computed
  stepIdx = computed(() => STEPS.findIndex(s => s.id === this.step()));

  filteredOffers = computed(() => filterOffers(this.allOffers(), this.rules()));

  previewOffers = computed(() => {
    if (this.colMode() === 'pick' && this.collectionOffers().length > 0) {
      return this.collectionOffers().slice(0, this.rules().limit || 6);
    }
    return this.filteredOffers();
  });

  selectedTheme = computed(() => this.themes().find(t => t.id === this.selectedThemeId()) ?? null);

  publishSummaryCollection = computed(() => {
    if (this.colMode() === 'pick') {
      const col = this.collections().find(c => c.id === this.selectedColId());
      return col ? `${col.name} · ${this.previewOffers().length} оферти` : 'Не е избрана';
    }
    return `${this.rules().name} · ${this.filteredOffers().length} оферти`;
  });

  canGetEmbed = computed(() => this.createdKey() !== null);

  sliderBg = computed(() => {
    const pct = ((this.rules().priceMax - 300) / (3500 - 300)) * 100;
    return `linear-gradient(90deg, var(--od-teal-600, #1a5a61) ${pct}%, var(--od-200, #e1e6e4) ${pct}%)`;
  });

  previewVars = computed(() => {
    const pt = PREVIEW_THEMES.find(t => t.v === this.previewTheme()) ?? PREVIEW_THEMES[0];
    return { background: pt.bg };
  });

  cardVars = computed(() => {
    const pt = PREVIEW_THEMES.find(t => t.v === this.previewTheme()) ?? PREVIEW_THEMES[0];
    return { 'border-radius': `${pt.radius}px` };
  });

  ctaVars = computed(() => {
    const pt = PREVIEW_THEMES.find(t => t.v === this.previewTheme()) ?? PREVIEW_THEMES[0];
    return { background: pt.accent };
  });

  gridVars = computed(() => {
    const cols = this.device() === 'mobile' ? 1 : this.exp().columns;
    return { 'grid-template-columns': `repeat(${cols}, 1fr)` };
  });

  searchGridVars = computed(() => {
    const cols = this.device() === 'mobile' ? 1 : Math.min(this.exp().columns, 2);
    return { 'grid-template-columns': `repeat(${cols}, 1fr)` };
  });

  constructor() {
    // When a collection is picked, load its resolved offers for the preview
    effect(() => {
      const id = this.selectedColId();
      if (id && this.colMode() === 'pick') {
        this.api.resolveCollection(id).subscribe({
          next: offers => this.collectionOffers.set(offers),
          error: () => this.collectionOffers.set([]),
        });
      } else {
        this.collectionOffers.set([]);
      }
    });
  }

  ngOnInit() {
    this.api.listCollections().subscribe({
      next: cols => {
        this.collections.set(cols);
        if (cols.length > 0) this.selectedColId.set(cols[0].id);
        this.loadingData.set(false);
      },
      error: () => this.loadingData.set(false),
    });
    this.api.listOffers().subscribe({
      next: offers => this.allOffers.set(offers),
      error: () => {},
    });
    this.api.listThemes().subscribe({
      next: themes => {
        this.themes.set(themes);
        const published = themes.find(t => t.status === 'Published');
        if (published) this.selectedThemeId.set(published.id);
        else if (themes.length > 0) this.selectedThemeId.set(themes[0].id);
      },
      error: () => {},
    });
  }

  goStep(id: StepId) { this.step.set(id); }

  onNextOrPublish() {
    const idx = this.stepIdx();
    if (idx < STEPS.length - 1) {
      this.step.set(STEPS[idx + 1].id);
    } else {
      this.doPublish();
    }
  }

  pickCollection(id: string) { this.selectedColId.set(id); }

  setRule(key: keyof BuilderRules, value: unknown) {
    this.rules.update(r => ({ ...r, [key]: value }));
  }

  toggleArr(key: 'countries' | 'boards' | 'months', val: string) {
    this.rules.update(r => {
      const arr = r[key] as string[];
      return { ...r, [key]: arr.includes(val) ? arr.filter(x => x !== val) : [...arr, val] };
    });
  }

  setExp(key: keyof ExperienceConfig, value: unknown) {
    this.exp.update(e => ({ ...e, [key]: value }));
  }

  addDomain() {
    const d = this.domainInput().trim();
    if (d && !this.allowedDomains().includes(d)) {
      this.allowedDomains.update(arr => [...arr, d]);
    }
    this.domainInput.set('');
  }

  removeDomain(d: string) {
    this.allowedDomains.update(arr => arr.filter(x => x !== d));
  }

  getVal(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  safeImg(url: string): string {
    return url ? `url(${url})` : '';
  }

  chIcon(id: string): string {
    if (id === 'wc')  return '</>';
    if (id === 'wp')  return 'W';
    if (id === 'api') return '⚡';
    return '○';
  }

  private doPublish() {
    this.error.set(null);

    if (this.colMode() === 'pick') {
      const colId = this.selectedColId();
      if (!colId) {
        this.error.set('Изберете колекция преди публикуване.');
        return;
      }
      this.callCreatePublication(colId);
    } else {
      // Rules mode: create collection first, then publication
      const r = this.rules();
      const filter: CreateCollectionRequest['filter'] = { all: [], any: [] };

      if (r.countries.length > 0) filter.all!.push({ field: 'country', op: 'in', value: r.countries });
      if (r.boards.length === 1)  filter.all!.push({ field: 'board',   op: 'eq', value: r.boards[0] });
      if (r.boards.length > 1)    r.boards.forEach(b => filter.any!.push({ field: 'board', op: 'eq', value: b }));
      if (r.transport !== 'any')  filter.all!.push({ field: 'transport', op: 'eq', value: r.transport });
      filter.all!.push({ field: 'maxPrice', op: 'lte', value: r.priceMax });

      const slug = slugify(r.name) + '-' + Date.now().toString(36);
      const req: CreateCollectionRequest = {
        agencyId: CURRENT_AGENCY_ID,
        name: r.name,
        slug,
        filter,
        sort: { field: r.sort === 'price' || r.sort === 'priceD' ? 'price' : 'price', direction: r.sort === 'priceD' ? 'desc' : 'asc' },
      };

      this.publishing.set(true);
      this.api.createCollection(req).subscribe({
        next: col => this.callCreatePublication(col.id),
        error: e => { this.error.set(e.error?.detail ?? e.message ?? 'Грешка при създаване на колекция.'); this.publishing.set(false); },
      });
    }
  }

  private callCreatePublication(collectionId: string) {
    this.publishing.set(true);
    const req: CreatePublicationRequest = {
      agencyId: CURRENT_AGENCY_ID,
      collectionId,
      themeId: this.selectedThemeId() ?? undefined,
      experienceConfig: this.exp(),
      allowedDomains: this.allowedDomains(),
    };

    this.api.createPublication(req).subscribe({
      next: pub => {
        if (!this.isDraft()) {
          this.api.publishPublication(pub.id).subscribe({
            next: published => { this.createdKey.set(published.key); this.publishing.set(false); this.embedOpen.set(true); },
            error: e => { this.error.set(e.error?.detail ?? 'Грешка при публикуване.'); this.publishing.set(false); },
          });
        } else {
          this.createdKey.set(pub.key);
          this.publishing.set(false);
          this.embedOpen.set(true);
        }
      },
      error: e => { this.error.set(e.error?.detail ?? e.message ?? 'Грешка при публикуване.'); this.publishing.set(false); },
    });
  }
}
