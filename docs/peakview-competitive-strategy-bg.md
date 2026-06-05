# PeakView: конкурентен анализ и стратегия за Odisea

## 1. Обхват и начин на работа

Този документ е жив регистър за конкурентно разузнаване и продуктова стратегия.
Анализът по-долу се базира на предоставената информация и екранни снимки от
PeakView към 4 юни 2026 г., както и на текущото състояние на кода на Odisea.

Използваме три нива на сигурност:

- **Наблюдение**: директно видима или описана функционалност на PeakView.
- **Извод**: вероятно техническо или бизнес обяснение на наблюдението.
- **Решение за Odisea**: какво и защо трябва да изградим ние.

Целта не е да копираме PeakView екран по екран. Целта е да разберем работещия им
бизнес модел и да предложим по-гъвкава платформа: един каталог, много канали за
дистрибуция, богата библиотека от компоненти и пълен контрол върху стила.

## 2. Кратко заключение

PeakView продава преди всичко **дистрибуция на туристическо съдържание**:

1. Събира оферти от туроператори.
2. Позволява на агенции да филтрират офертите в White Label конфигурации.
3. Вгражда резултата в сайтовете на агенциите.
4. Продава отделни интеграции с външни доставчици.
5. Допълва продукта с back-office документи, сайтове, хостинг и поддръжка.

Odisea вече има основата на най-важната им способност:

- `Offer` е нормализирана оферта.
- `Collection` е по-силен аналог на White Label.
- `FilterSpec`, pinned и excluded оферти са основа за гъвкава селекция.
- Lit web component е модерна алтернатива на iframe.

Най-голямата възможност за Odisea е да стане **headless travel commerce и
styling платформа**, която работи еднакво добре за хора без технически знания,
за WordPress/Shopify потребители и за разработчици.

## 3. Продуктова карта на PeakView

### 3.1. B2B каталог и marketplace

**Наблюдения**

- PeakView показва хиляди оферти от над сто оператора.
- Агенциите могат да зареждат оферти на оператори в собствените си сайтове.
- Туроператорите могат да публикуват чрез админ панел или XML интеграция.
- Има новини и бюлетини от туроператори.

**Извод**

PeakView е двустранна мрежа:

- supply side: туроператори и външни доставчици;
- distribution side: туристически агенции и техните сайтове.

Каталогът и партньорската мрежа са по-важни от самия embed код. Без достатъчно
доставчици компонентите нямат стойност.

### 3.2. White Label / Label builder

**Наблюдения**

- Агенцията създава White Label с име.
- Избира туроператор, държава, транспорт и курорт.
- Може да комбинира оферти от няколко оператора.
- Получава код за вграждане.
- Може да конфигурира цветове или да добави собствен CSS.

**Извод**

White Label е запазена заявка към каталога плюс визуална конфигурация и embed
идентификатор. Текущият `Collection` модел на Odisea вече е добър фундамент,
но визуалната конфигурация трябва да бъде отделен модел, а не част от самата
колекция.

### 3.3. Специализирани търсачки и iframe продукти

**Наблюдения**

- Отделни търсачки за хотели в България, Гърция, Турция и екзотични дестинации.
- Отделен iframe за яхтен туризъм.
- Използва се iframe плюс `iframeResizer`.
- Някои търсачки са безплатни при активен абонамент и регистрация към доставчик.

**Извод**

Това са вертикални продукти върху конкретни доставчици, не непременно еднакви
функционалности. PeakView използва нискорисков начин за дистрибуция: iframe
изолира стила и интеграционните различия, но ограничава SEO, UX и персонализацията.

### 3.4. XML интеграции

**Наблюдения**

- PeakView импортира готов XML от туроператори.
- Предлага изработка на XML, когато операторът няма такъв.
- Продава отделни интеграции към доставчици като TravelPlan, Go2Holiday,
  Sunny Holidays Greece, Solvex и други.
- Интеграциите имат еднократна цена и годишен абонамент.

**Извод**

Интеграционният слой е основен revenue stream. Вероятно всяка връзка има
специфично преобразуване към вътрешен формат и периодична синхронизация.

### 3.5. Travel Web Office

**Наблюдения**

- Резервациите се оформят като договори.
- Генерират се фактури, застраховки, ваучери, rooming lists и други документи.
- Сайтът на агенцията се превръща в работна платформа.

**Извод**

Това е отделен operational/back-office продукт. Той е значително по-сложен от
каталог и embed компоненти и включва резервационен lifecycle, документи,
финансови данни, права и audit trail.

### 3.6. Уеб услуги и SEO label

**Наблюдения**

- Изработка на responsive сайтове.
- Поддръжка, редизайн, SEO, хостинг и домейни.
- SEO label, чрез който офертите стават част от сайтовете на агенти и могат да
  бъдат индексирани.

**Извод**

PeakView комбинира SaaS с agency услуги. SEO label вероятно използва server-side
или native page rendering, защото iframe съдържанието само по себе си не дава
надеждна SEO стойност на сайта домакин.

### 3.7. Профили, абонаменти и onboarding

**Наблюдения**

- Има профил на фирма с ЕИК, МОЛ, контакти и адреси.
- Има активен абонамент с дата на изтичане.
- Формулярът за участие квалифицира нуждите на клиента.
- Има различни предложения за агенции и туроператори.

**Извод**

Има поне базово tenant, role и subscription моделиране. Формулярът за участие
служи едновременно за lead generation и assisted onboarding.

## 4. Бизнес модел и конкурентни предимства

### Приходни потоци на PeakView

- Годишен B2B абонамент за агенции.
- Setup и годишен абонамент за туроператори.
- Еднократни такси за специфични XML интеграции.
- Travel Web Office: инсталация плюс годишен абонамент.
- Хостинг, поддръжка, домейн, SEO и изработка на сайтове.

### Реални силни страни

- Съществуваща мрежа от оператори и агенции.
- Голям обем актуално съдържание.
- Познаване на локалния туристически пазар.
- Assisted service модел: клиентът може да плати и да получи готово решение.
- Ниска бариера за embed чрез copy/paste iframe.

### Видими слабости и възможности за Odisea

- Ограничен и остарял UX.
- Стилизирането е основно цветове плюс свободен CSS.
- iframe подходът ограничава native UX, analytics, SEO и композирането.
- Интеграциите изглеждат като отделни продукти вместо единна connector платформа.
- Не се виждат versioned themes, reusable component presets, preview environments
  или no-code page composition.
- Не се вижда developer-first API/SDK продукт.

## 5. Позициониране на Odisea

Препоръчана продуктова теза:

> Odisea е платформа за дистрибуция и представяне на туристически продукти.
> Туроператорите публикуват веднъж. Агенциите избират, стилизират и публикуват
> навсякъде чрез готови компоненти, plugins или API.

Трябва да обслужим три типа потребители:

| Потребител | Какво иска | Основен интерфейс |
|---|---|---|
| Нетехническа агенция | готова секция в сайта без код | visual builder + plugin/block |
| Агенция с разработчик | пълен стил и контрол върху поведението | web components + theme tokens + events |
| Enterprise/платформа | собствен frontend и автоматизация | versioned REST API, webhooks и SDK |

Ключовото обещание не е само „вгради оферти“, а:

**Избери съдържание, избери преживяване, избери стил, публикувай във всеки канал.**

## 6. Целева продуктова архитектура

### 6.1. Разделяне на основните понятия

Не трябва да натоварваме `Collection` с всички отговорности.

| Понятие | Отговорност |
|---|---|
| `Offer` | нормализиран туристически продукт/оферта |
| `SupplierConnection` | връзка и credentials към външен доставчик |
| `ImportJob` | конкретно изпълнение на синхронизация |
| `SourceOffer` | суров запис от доставчик за traceability |
| `Collection` | запазена селекция от оферти |
| `Experience` | композиция от компоненти и поведение |
| `Theme` | дизайн токени, typography, spacing и component variants |
| `Publication` | публикувано съчетание от Collection + Experience + Theme |
| `ChannelInstallation` | инсталация в сайт, WordPress, Shopify или custom app |
| `Lead` / `BookingRequest` | интерес или заявка от краен клиент |

Това разделяне позволява една колекция „Почивки в Гърция“ да се използва като:

- grid на начална страница;
- carousel;
- търсачка;
- landing page;
- WordPress block;
- JSON API feed;
- iframe fallback.

### 6.2. Component platform

Първата библиотека трябва да включва:

- `od-offer-card`
- `od-offer-grid`
- `od-offer-carousel`
- `od-offer-search`
- `od-filter-panel`
- `od-offer-details`
- `od-price-table`
- `od-booking-inquiry`
- `od-destination-hero`
- `od-featured-collections`

Компонентите трябва да имат:

- CSS custom properties за всички дизайн токени;
- named parts чрез `::part()` за напреднало стилизиране;
- slots за контролирано заместване на съдържание;
- документирани browser events;
- loading, empty и error states;
- accessibility и responsive поведение;
- versioned runtime и обратно съвместима конфигурация;
- telemetry, която може да се изключва според consent настройките.

### 6.3. Theme system

PeakView дава списък с цветове. Odisea трябва да даде цяла дизайн система:

- foundation tokens: цветове, typography, spacing, radius, shadow;
- semantic tokens: primary action, price, promotion, surface, border;
- component tokens: card image ratio, button variant, filter layout;
- responsive tokens;
- theme presets и наследяване;
- draft/published версии;
- live preview върху desktop, tablet и mobile;
- export като CSS variables и JSON tokens;
- custom CSS като напреднала escape hatch, не като основен интерфейс.

### 6.4. Канали за публикуване

Поддържаме еднакъв publication contract, но различни adapters:

1. **Copy/paste embed**: script + custom element.
2. **Iframe fallback**: за среди, които блокират scripts или имат CSS конфликти.
3. **WordPress plugin**: Gutenberg blocks и shortcode.
4. **Shopify app extension**: theme app blocks.
5. **JavaScript package**: за npm приложения.
6. **REST API и webhooks**: за custom/enterprise frontends.
7. **SEO publishing adapter**: генерирани или синхронизирани native страници.

Важно: Shopify не трябва да се третира като типичен туристически канал в първия
roadmap само защото е популярен. Трябва първо да валидираме реално търсене.
WordPress вероятно е по-висок приоритет за българските туристически агенции.

### 6.5. Connector platform

Вместо „нова XML интеграция“ като отделна импровизация всеки път:

- connector contract: fetch, parse, validate, normalize, upsert, deactivate;
- adapters за XML, JSON API, CSV/SFTP и manual entry;
- mapping profiles по доставчик;
- scheduler и retry policy;
- import preview преди активиране;
- dead-letter/error queue за невалидни записи;
- freshness, последна успешна синхронизация и health dashboard;
- source lineage върху всяка оферта;
- idempotent imports и deduplication;
- alerts при голям спад на оферти или промяна на schema.

В първите етапи това може да остане в modular Clean Architecture приложението.
Не е необходима microservice архитектура преди реално натоварване.

### 6.6. SEO модел

SEO не трябва да се обещава чрез iframe или client-only component.

Нужни са два режима:

- **Embed mode**: бърз, интерактивен, подходящ за съществуваща страница.
- **SEO publishing mode**: native страници с canonical URL, structured data,
  sitemap, metadata и server-rendered/предварително генерирано съдържание.

SEO publishing може да започне с WordPress sync/plugin, преди да изграждаме
универсален rendering service.

### 6.7. Leads, заявки и резервации

Трябва да разграничим:

- `Lead`: клиентски интерес и контакт;
- `BookingRequest`: структурирана заявка за конкретна оферта;
- `Reservation`: потвърдена резервация;
- `Document`: договор, ваучер, фактура, rooming list;
- `Payment`: отделен финансов lifecycle.

Първо изграждаме lead и booking request потока. Пълен Travel Web Office трябва
да бъде отделна продуктова фаза след валидиране, защото носи повече регулаторна,
финансова и operational сложност.

## 7. Gap анализ спрямо текущия код

### Какво вече е на правилното място

- Clean Architecture и MVC controller моделът позволяват постепенно разширяване.
- `Offer`, `Agency`, `Operator` и `Collection` са правилно начално ядро.
- `Collection.Filter`, `Sort`, pinned и excluded оферти покриват основата на
  PeakView White Label.
- `ParameterDef` подготвя reusable колекции с runtime параметри.
- Lit със Shadow DOM е подходящ за framework-independent embeds.
- CSS custom properties вече доказват базовото theming направление.

### Критични липси преди реални клиенти

- Authentication, authorization, tenant isolation и ownership проверки.
- Реални отношения и договори между агенции и оператори.
- Subscription/entitlement модел.
- Supplier connectors и import lifecycle.
- Offer source identity, freshness и деактивация.
- Collection builder и publication workflow.
- Theme и Experience модели.
- API keys, allowed domains и embed security.
- Analytics, leads и attribution.
- Versioning и backward compatibility за embed runtime.

### Конкретни рискове в текущия модел

- `Collection.Slug` е глобално уникален; при multi-tenant продукт трябва да бъде
  уникален в рамките на agency/tenant или publication namespace.
- `Collection` е директно вързан към `AgencyId`; трябва да уточним дали оператор
  може да създава reusable collections/templates.
- `SupplierId` в `Offer` не описва source connection, external ID или import state.
- Една цена и един date range няма да покрият хотелски таблици, варианти,
  departure dates и различни room/board комбинации.
- `Tags` като JSON са достатъчни за scaffold, но трябва да се оцени търсене и
  индексиране при реален обем.
- Текущият компонент има само offer grid поведение въпреки свойството `layout`.
- Няма стабилен публичен publication identifier; директното излагане на collection
  slug смесва вътрешно управление с публична дистрибуция.
- CORS и публичните collection endpoints не са достатъчна embed security политика.

## 8. Препоръчан roadmap

### Фаза 0: Product discovery и договори

Цел: да не изградим грешна универсалност.

- Интервюта с 5-10 агенции и 3-5 туроператора.
- Събиране на реални XML/API примери от поне два доставчика.
- Карта на най-честите WordPress/site-builder среди.
- Дефиниране на canonical offer schema и publication contract.
- Валидиране кои действия са lead, request и реална booking операция.

**Изход:** schema v1, integration contract v1 и приоритизиран component catalog.

### Фаза 1: Odisea White Label MVP

Цел: да надминем основния PeakView White Label flow.

- Tenant auth и роли: platform admin, operator admin, agency admin/editor.
- Collection builder с operator, country, destination и transport filters.
- Preview, publish/unpublish и embed code generator.
- `Publication` модел с публичен стабилен ID.
- `Theme` модел с tokens, presets и live preview.
- Компоненти: grid, carousel, search/filter и offer details.
- Allowed domains, API key/public token и основни analytics events.

**Изход:** агенция без разработчик създава и публикува branded offer experience.

### Фаза 2: Supply и connector engine

Цел: да превърнем съдържанието в устойчив конкурентен актив.

- Manual operator portal за оферти.
- Connector contract и първи два реални XML/API adapters.
- SourceOffer, import jobs, mapping, validation и health dashboard.
- Freshness/deactivation правила.
- Operator-to-agency access и commercial entitlement правила.

**Изход:** реални операторски оферти се синхронизират надеждно и се дистрибутират.

### Фаза 3: No-code distribution ecosystem

Цел: публикуване във възможно най-много реални клиентски среди.

- WordPress plugin с blocks/shortcodes и SEO sync режим.
- Theme marketplace/library и component presets.
- Landing page templates.
- Install wizard и диагностика.
- Developer docs, npm package, events и webhooks.
- Iframe fallback generator.

**Изход:** клиентите публикуват без custom development, а разработчиците имат
пълен контрол.

### Фаза 4: Conversion и office capabilities

Цел: Odisea да участва в приходния процес, не само в показването.

- Lead и booking request inbox.
- CRM/webhook integrations.
- Quote и reservation lifecycle.
- Document templates и generation.
- Audit log, permissions и GDPR retention.
- Фактури, ваучери, застраховки и rooming lists само след правен и operational
  анализ.

**Изход:** измерима конверсия и постепенно изграждане на Travel Office продукт.

## 9. Приоритети: какво да не правим още

- Да не започваме с пълен Travel Web Office.
- Да не обещаваме „директна резервация“, преди да разбираме supplier contract-а.
- Да не правим отделен hardcoded продукт за всяка държава или доставчик.
- Да не използваме custom CSS като основен no-code theming интерфейс.
- Да не изграждаме plugins за всички CMS едновременно.
- Да не разделяме connector engine в microservices преждевременно.
- Да не смесваме вътрешна `Collection` идентичност с публична `Publication`.

## 10. Препоръчана следваща техническа работа

Следващият development increment трябва да бъде **Publication + Theme foundation**,
а не просто разширение на текущия offer card.

Предложен тесен vertical slice:

1. Добавяне на `Theme` с versioned design tokens.
2. Добавяне на `Publication`, която сочи към `Collection` и `Theme`.
3. Публичен endpoint `/api/v1/publications/{key}` с publication manifest.
4. Промяна на loader/component-а да приема `publication`, не директно `collection`.
5. Portal екран за theme editor, live preview и copy/paste embed code.
6. Запазване на текущия collection endpoint като вътрешна management способност.

Този slice създава правилната продуктова граница за всички бъдещи компоненти,
WordPress plugin, iframe fallback, analytics и versioning.

## 11. Въпроси за следващо конкурентно проучване

При следващ достъп или информация от PeakView трябва да установим:

- Как изглежда реалният embed на White Label и offer detail страницата?
- Отваря ли офертата PeakView URL, agency URL или operator URL?
- Има ли lead/booking form и кой получава заявката?
- Как се определя комисионната и достъпът на агенция до оператор?
- Колко често се синхронизират XML източниците?
- Как се обработват изтрити, изтекли и променени оферти?
- Как работи SEO label технически?
- Има ли analytics и отчети за clicks, leads и bookings?
- Има ли роли, служители и audit log в профила?
- Какво точно включва Travel Web Office workflow?
- Как изглеждат ценовите таблици, departure dates и availability?
- Какво може да персонализира агенцията отвъд цветовете и custom CSS?

## 12. Метрики за успех на Odisea

- Време от регистрация до първи публикуван embed.
- Процент клиенти, публикували без помощ от екипа.
- Брой активни publications на агенция.
- Freshness и success rate на supplier imports.
- Component impressions, offer opens и lead conversion.
- Брой използвани themes/presets.
- Процент WordPress инсталации спрямо generic embed.
- Retention на агенции и активни оператори.
- Време за добавяне на нов connector.
- Приход на tenant и разход за assisted onboarding.
