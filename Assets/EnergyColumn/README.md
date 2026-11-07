# Полный гайд: колонна с текущей энергией — из Blender в Unity (URP)

Версия гайда 2 (переписан полностью, главный путь — без сохранения файлов).
Репозиторий: **Elektrobog/ProjectSphere**, ветка **energy-column**, папка **Assets/EnergyColumn**.

---

## 0. Принцип — что вообще происходит

Колонна = геометрия (плиты, стеклянные трубки, стержни энергии) + анимация энергии.

- **FBX переносит только геометрию** и имена материалов. Шейдерная анимация из Blender в FBX не едет — так устроен формат.
- Поэтому течение энергии воссоздано **шейдером в Unity** (SciFiEnergyColumn.shader): те же бегущие зелёные «капли» и искры, но живут они на игровом времени.
- **Animator не нужен. Ключей анимации нет. Запечённых кадров нет.** Работает на ПК и мобилках, стоит копейки по производительности.

Всего будет 4 этапа: забрать код → Blender (сгенерировать FBX) → Unity (импорт + материалы) → Bloom.

---

## 1. Что понадобится

| Что | Зачем |
|---|---|
| Blender 3.0 или новее | сгенерировать колонну и FBX |
| Unity (ProjectSphere, URP) | игра |
| Код скрипта BlenderSciFiColumn.py | из репо, ветка energy-column |
| Код шейдера SciFiEnergyColumn.shader | из репо, ветка energy-column |

Код берётся из репозитория. **Сохранять его файлами на диск НЕ обязательно** — есть путь попроще (см. шаг 1).

---

## 2. Этап 1 — забрать код (2 способа)

### Способ А — БЕЗ файлов (рекомендую, самый надёжный)

Проблема способа Б: Блокнот в Windows любит дописывать .txt к имени файла, и тогда Blender/Unity файл не видят. Способ А полностью обходится без сохранения файлов.

**Скрипт для Blender (живёт прямо внутри .blend):**

1. Открой в браузере: репозиторий Eleketrobog/ProjectSphere → ветка **energy-column** → файл **Assets/EnergyColumn/BlenderSciFiColumn.py**.
2. Нажми кнопку **Raw** — откроется чистый текст.
3. Ctrl+A (выделить всё) → Ctrl+C (скопировать).
4. Дальше сразу в Blender — см. этап 2, шаг «Запуск»: там текст просто вставляется в редактор скриптов.

**Шейдер для Unity (файл создаётся средствами самой Unity):**

1. Открой в браузере тот же файл **Assets/EnergyColumn/SciFiEnergyColumn.shader** → **Raw** → Ctrl+A → Ctrl+C.
2. В Unity: в окне Project создай папку: правый клик по Assets → **Create → Folder** → назови **EnergyColumn**.
3. Правый клик по папке → **Create → Shader → Unlit Shader** → назови **SciFiEnergyColumn** (без слова Shader на конце — Unity добавит его сам).
4. **Двойной клик** по этому шейдеру — откроется редактор кода (Visual Studio или другой). Выдели всё (Ctrl+A), удали, вставь скопированный текст (Ctrl+V), сохрани (Ctrl+S).
5. Вернись в Unity — она сама скомпилирует шейдер. Если в нижнем баре нет красных ошибок — всё получилось.

### Способ Б — файлами с диска (если удобнее)

1. Открой страницу файла в ветке energy-column → **Raw** → Ctrl+A → Ctrl+C.
2. Открой **Блокнот** → вставь (Ctrl+V).
3. **Файл → Сохранить как**:
   - Имя файла набери **в кавычках**: "BlenderSciFiColumn.py" (кавычки не дадут Блокноту дописать .txt);
   - Тип файла: **Все файлы (*.*)**;
   - Кодировка: **UTF-8**.
4. Для шейдера то же самое, только имя "SciFiEnergyColumn.shader".

Проверка, что файл сохранился правильно:

1. В проводнике Windows: **Вид → галочка «Расширения имён файлов»**.
2. Имя должно быть ровно **BlenderSciFiColumn.py**. Если видно **BlenderSciFiColumn.py.txt** — файл спасён неправильно: нажми F2 и убери .txt.

Если у тебя установлен git — можно просто переключиться на ветку energy-column, тогда оба файла уже лежат в Assets/EnergyColumn и ничего копировать не надо.

---

## 3. Этап 2 — Blender: генерация колонны и FBX

### 3.1. Подготовка (ВАЖНО)

**Скрипт при запуске стирает ВСЁ со сцены** — все объекты текущего .blend-файла. Поэтому:

1. Если в открытом файле есть что-то ценное — сначала File → Save As, сохрани его под другим именем.
2. Лучше начать с чистого: **File → New → General**.
3. **Сохрани новый .blend** (Ctrl+S) в удобную папку.

Зачем сохранять: FBX-файл скрипт кладёт **рядом с .blend**. Не сохранишь .blend — FBX уедет в домашнюю папку и потом его придётся искать.

### 3.2. Запуск (для способа А — вставка текста)

1. Вверху окна Blender открой вкладку **Scripting**.
2. Нажми кнопку **New** — появится пустой текстовый блок.
3. Кликни в поле текста → **Ctrl+V** (вставь скопированный код).
4. Нажми **▶ Run Script** — кнопка над текстом.

Для способа Б: Scripting → **Open** → выбери файл BlenderSciFiColumn.py → Run Script.

### 3.3. Что произойдёт (2–5 секунд)

- Бронеплиты, стеклянные трубки, стержни энергии, центральный корпус.
- 5 материалов: Energy - Flow Vertical, Energy - Flow Ring, Transparent Glass - Tubes, Graphite Blue - Armor, Inner Body - Dark Blue.
- Камера и свет для превью.
- Скрипт сам конвертирует кривые в меши (FBX не понимает кривые Blender) и сохранит **SciFiEnergyColumn.fbx рядом с .blend**.

Где смотреть путь к FBX: **Window → Toggle System Console** (Windows) — в конце будет строка «FBX для Unity сохранён: ...».

### 3.4. Проверка превью (по желанию)

1. Наведи мышь на 3D-вьюпорт → **Z** → **Material Preview** (или Rendered).
2. **Space** — по трубкам побегут зелёные капли, по кольцам — искры.

Если не двигается — нажми Space ещё раз (это play/pause). Это превью в Blender; в Unity будет то же самое из шейдера.

### 3.5. Как менять модель

Все размеры — числами в начале скрипта (блок «1. ПАРАМЕТРЫ»): HEIGHT (высота), DIAMETER (диаметр), VERTICAL_LINES (трубок вокруг), HORIZONTAL_ROWS (колец по высоте). Изменил → Run Script ещё раз → FBX пересоздался сам.

---

## 4. Этап 3 — Unity: импорт модели

1. Найди на диске **SciFiEnergyColumn.fbx** (лежит рядом с .blend).
2. Перетащи его мышкой в окно **Project** Unity (внутрь Assets). Альтернатива: Assets → правый клик → Import New Asset.
3. Выбери модель → Inspector → вкладка **Model**:
   - **Scale Factor = 1**;
   - **Normals = Import**;
   - **Apply**.
4. Перетащи модель из Project в **Hierarchy** — колонна в сцене.

Размер: 1 юнит Unity = 1 метр, колонна по умолчанию 10 м высотой. Мало? Выдели объект в Hierarchy и поставь Scale 0.5 (получится 5 м).

Если модель импортировалась гигантской (в 100 раз больше) — вкладка Model → включи **Convert Units** → Apply. Если крошечной — выключи Convert Units.

---

## 5. Этап 3.5 — Unity: извлечение материалов

Unity видит имена материалов из FBX, но в проект их нужно вытащить:

1. Выбери FBX-модель в Project → Inspector → вкладка **Materials**.
2. **Extract Materials...** → выбери папку (удобно Assets/EnergyColumn/Materials).
3. Появятся 5 материалов: Energy - Flow Vertical, Energy - Flow Ring, Transparent Glass - Tubes, Graphite Blue - Armor, Inner Body - Dark Blue.

Если кнопки Extract нет (старая версия Unity): создай 5 материалов вручную (правый клик → Create → Material, те же имена) и назначь их мешам модели.

---

## 6. Этап 4 — Unity: настройка 5 материалов

Как назначить шейдер: клик по материалу → Inspector → сверху выпадающий список **Shader** → в поиске набери **EnergyFlow** → выбери **ProjectSphere/EnergyFlow**.

### 6.1. Energy - Flow Vertical — энергия в вертикальных трубках

| Параметр | Значение |
|---|---|
| Shader | ProjectSphere/EnergyFlow |
| Flow Mode | **Vertical Tube** |

Остальное уже настроено рабочими значениями, ничего менять не надо.

### 6.2. Energy - Flow Ring — энергия в кольцах

| Параметр | Значение |
|---|---|
| Shader | ProjectSphere/EnergyFlow |
| Flow Mode | **Horizontal Ring** |
| Ring Radius | 1.46 |

### 6.3. Transparent Glass - Tubes — стекло

| Параметр | Значение |
|---|---|
| Shader | Universal Render Pipeline/Lit |
| Surface Type | **Transparent** |
| Rendering Face | **Both** |
| Base Color | (0.75, 0.88, 1.0), Alpha ≈ 0.15 |
| Smoothness | 0.95 (Metallic 0) |

### 6.4. Graphite Blue - Armor — бронеплиты

| Параметр | Значение |
|---|---|
| Shader | Universal Render Pipeline/Lit |
| Base Color | (0.12, 0.17, 0.23) |
| Metallic | 0.1 |
| Smoothness | 0.58 |

### 6.5. Inner Body - Dark Blue — центральный корпус

| Параметр | Значение |
|---|---|
| Shader | Universal Render Pipeline/Lit |
| Base Color | (0.025, 0.045, 0.07) |
| Metallic | 0.15 |
| Smoothness | 0.45 |

Проверка: энергия потечёт сразу после назначения шейдера — без Bloom, просто без ореола.

---

## 7. Этап 5 — Bloom (свечение капель)

Без Bloom капли просто ярко-зелёные. Мягкий ореол даёт постобработка. Нужны ТРИ переключателя:

### 7.1. URP Asset

1. Edit → Project Settings → Graphics → посмотри имя назначенного **URP Asset** (обычно в Assets/Settings).
2. Открой его → галочки **Post Processing** и **HDR**.

### 7.2. Global Volume в сцене

1. GameObject → Volume → **Global Volume**.
2. Inspector → **Add Override** → Post Processing → **Bloom**: Threshold ≈ 0.8, Intensity ≈ 1.5.

### 7.3. Камера

1. Выбери Main Camera → Inspector → Rendering → галочка **Post Processing**.

Проверка в Game view: у капель появился мягкий зелёный ореол — готово.

---

## 8. Синхронизация с Blender-скриптом (если менял размеры)

| Параметр шейдера | Откуда в скрипте |
|---|---|
| Core Radius (Vertical) | ENERGY_RADIUS = 0.018 |
| Ring Radius (Ring) | ≈ DIAMETER / 2 − 0.04 (по умолчанию 1.46) |

Изменил DIAMETER — обнови Ring Radius в материале Energy - Flow Ring.

---

## 9. Параметры шейдера — настройка под себя

| Параметр | Что делает | По умолчанию | Рецепты |
|---|---|---|---|
| Flow Speed | скорость течения | 4.0 (~1.4 м/с) | быстрее — 6–8; медленнее — 2 |
| Blob Length | длина капли, м | 0.35 | крупные — 0.5; мелкие частые — 0.2 |
| Blob Contrast | резкость капель | 1.7 | резче — 2.0–3.0 |
| Min Brightness | свечение пауз | 0 | максимум 0.1, иначе снова «палка» |
| Filaments Around Core | прожилки вокруг стержня | 3 | 2–4 |
| Spark Brightness | быстрые искры | 1.5 | 0 = выключить |
| Spark Period | шаг искр, м | 1.5 | меньше — чаще |
| Overall Brightness | общая яркость | 3 | должна пробивать Threshold Bloom (0.8) |
| Mid Color / Hot Color | цвет капли / сердцевины | зелёный | любой цвет (оранжевый = «лава») |

---

## 10. Проблемы и решения

| Проблема | Причина | Решение |
|---|---|---|
| Файл .py не виден в проводнике Blender | Блокнот сохранил как .py.txt | Способ А (вставка текста, раздел 2) или Вид → Расширения имён файлов → переименовать без .txt |
| Шейдер не появляется в списке Shader | файл .shader не в Assets / с .txt | создай шейдер через Unity (Create → Shader), вставь текст (раздел 2, способ А) |
| Трубка — сплошная зелёная палка | Min Brightness > 0 или мал Contrast | Min Brightness = 0, Contrast = 1.7–2.5 |
| Капли не светятся ореолом | Bloom не включён | все ТРИ переключателя из раздела 7 |
| В Scene светится, в Game — нет | на камере нет галочки Post Processing | Main Camera → Rendering → Post Processing |
| Модель гигантская / крошечная | единицы FBX | вкладка Model → Convert Units вкл/выкл → Apply |
| Все трубки текут синхронно | Static Batching | снять галочку Static с трубок |
| Трубки чёрные или ярко-розовые | шейдер не назначен материалу / ошибка компиляции | проверь шейдер в Assets/EnergyColumn без ошибок |
| FBX не появился | .blend не сохранён | сохрани .blend, запусти скрипт снова; путь — в System Console |
| На кольцах шов | неверный Ring Radius | Ring Radius ≈ DIAMETER / 2 − 0.04 |
| Энергия «плоская», без прожилок | неверный Core Radius | Core Radius = 0.018 |
| Ошибка при запуске скрипта | Blender < 3.0 или открыт диалог | обнови Blender; закрой модальные окна, повтори Run Script |

---

## 11. Производительность

- Энергия — Unlit-шейдер: без текстур, без освещения, без теней. Дёшев, подходит мобилкам.
- Для слабых устройств уменьши в Blender-скрипте: CIRCLE_SEGMENTS 192 → 64, VERTICAL_LINES 16 → 12, HORIZONTAL_ROWS 14 → 10.
- Трубки не ставить в Static (фаза течения берётся из позиции объекта, в шейдере уже стоит DisableBatching).

---

## 12. Шпаргалка

1. Код из репо (ветка energy-column): Raw → Ctrl+A → Ctrl+C.
2. Blender: Scripting → New → Ctrl+V → Run Script. FBX лежит рядом с .blend.
3. Unity: FBX в Assets → Scale 1 → Extract Materials.
4. Unity: Create → Shader → вставить текст шейдера → сохранить.
5. Energy-материалы: ProjectSphere/EnergyFlow (Vertical Tube / Horizontal Ring). Стекло/плиты/корпус: URP/Lit.
6. Bloom: URP Asset + Global Volume + галочка на камере.
7. Настройки: Flow Speed (скорость), Blob Length (капли), Min Brightness = 0.
