# Подробный гайд: колонна с текущей энергией — из Blender в Unity (URP)

Гайд для репозитория **Elektrobog/ProjectSphere**, ветка **energy-column**.
Папка с файлами: **Assets/EnergyColumn** (BlenderSciFiColumn.py, SciFiEnergyColumn.shader).

---

## 0. Как это устроено (принцип, 1 минута)

Колонна состоит из геометрии (плиты, стеклянные трубки, стержни энергии) и **анимации энергии**.

- **FBX-файл** переносит только геометрию и имена материалов.
- Анимация энергии из Blender (шейдерные узлы) в игры **не переносится** — так устроен FBX.
- Поэтому течение энергии воссоздано **шейдером в Unity** (SciFiEnergyColumn.shader): те же бегущие «капли» и искры, но живут они на времени игры. Animator, анимационные клипы и запечённые кадры не нужны.

Это стандартный подход в играх: дёшево, работает на ПК и мобилках.

---

## 1. Что понадобится

| Что | Зачем |
|---|---|
| Blender 3.x / 4.x / 5.x | сгенерировать колонну и FBX |
| Unity (проект ProjectSphere, URP) | сама игра |
| BlenderSciFiColumn.py | генератор колонны (в репо, ветка energy-column) |
| SciFiEnergyColumn.shader | шейдер энергии (в репо, там же) |

---

## 2. Шаг 1 — забрать файлы из репозитория (без git)

1. Открой в браузере страницу файла в ветке energy-column:
   - Assets/EnergyColumn/BlenderSciFiColumn.py
   - Assets/EnergyColumn/SciFiEnergyColumn.shader
2. Нажми кнопку **Raw** — откроется чистый текст файла.
3. Выдели всё (Ctrl+A), скопируй (Ctrl+C).
4. Вставь в любой текстовый редактор (Блокнот) и сохрани:
   - BlenderSciFiColumn.py
   - SciFiEnergyColumn.shader

При сохранении выбери «Тип файла: Все файлы», чтобы не получилось .txt.

Если git установлен — можно просто переключиться на ветку energy-column и открыть папку Assets/EnergyColumn.

---

## 3. Шаг 2 — Blender: подготовка

**Внимание!** Скрипт при запуске **полностью очищает сцену** (удаляет все объекты). Поэтому:

1. Если в текущем файле есть что-то ценное — сначала File → Save As и сохрани его под другим именем.
2. Лучше начать с чистого файла: File → New → General.
3. **Сохрани новый .blend** (Ctrl+S) в удобной папке, например «Проект Колонна».

Почему это важно: FBX-файл скрипт кладёт **рядом с .blend**. Если .blend не сохранён, FBX уедет в домашнюю папку, и его придётся искать.

---

## 4. Шаг 3 — Blender: запуск скрипта

1. Сверху окна Blender открой вкладку **Scripting**.
2. Нажми **Open** и выбери BlenderSciFiColumn.py.
3. Нажми кнопку **Run Script** (кнопка ▶ над текстом скрипта).

Что произойдёт (займёт пару секунд):
- создадутся бронеплиты, стеклянные трубки, стержни энергии, центральный корпус;
- материалы: Energy - Flow Vertical, Energy - Flow Ring, Transparent Glass - Tubes, Graphite Blue - Armor, Inner Body - Dark Blue;
- камера и свет для превью;
- **сразу после генерации** скрипт сам конвертирует кривые в меши и сохранит **SciFiEnergyColumn.fbx рядом с .blend**.

Путь к FBX печатается в консоль:
- Windows: меню Window → Toggle System Console;
- в конце будет строка «FBX для Unity сохранён: ...».

Если хочешь изменить модель (высота, диаметр, число трубок) — правь числа в блоке «1. ПАРАМЕТРЫ» в начале скрипта и нажми Run Script ещё раз. FBX пересоздастся.

---

## 5. Шаг 4 — Blender: проверка анимации (по желанию)

1. Наведи мышь на вьюпорт и нажми **Z** → выбери **Material Preview** или **Rendered**.
2. Нажми **Space** (запуск анимации) — по трубкам должны бежать зелёные капли с тёмными паузами, по кольцам — искры.

Это только превью в Blender. В Unity будет то же самое, но уже из шейдера SciFiEnergyColumn.shader.

Если превью не двигается — нажми Space ещё раз (Space переключает play/pause).

---

## 6. Шаг 5 — Unity: импорт модели

1. Найди на диске файл SciFiEnergyColumn.fbx (рядом с .blend).
2. Перетащи его мышой в окно **Project** Unity (внутрь Assets) — или Assets → правый клик → Import New Asset → выбери файл.
3. Выбери импортированную модель, в Inspector открой вкладку **Model**:
   - **Scale Factor = 1** (если модель потом окажется гигантской — включи галочку **Convert Units** и нажми Apply; если крошечной — выключи);
   - **Normals = Import**;
   - нажми **Apply**.
4. Перетащи модель из Project в окно **Hierarchy** — колонна появится в сцене.

Размер по умолчанию: высота 10 метров, диаметр 3 метра (1 юнит Unity = 1 метр). Для сцены игры можно уменьшить: например, Scale 0.5 = высота 5 метров.

---

## 7. Шаг 6 — Unity: извлечение материалов

При импорте FBX Unity видит имена материалов из Blender, но сами материалы нужно «извлечь» в проект:

1. Выбери FBX-модель в Project → Inspector → вкладка **Materials**.
2. Нажми **Extract Materials...** → выбери папку (удобно создать Assets/EnergyColumn/Materials).
3. В проекте появятся 5 материалов:
   - Energy - Flow Vertical
   - Energy - Flow Ring
   - Transparent Glass - Tubes
   - Graphite Blue - Armor
   - Inner Body - Dark Blue

Если версия Unity не показывает кнопку Extract — создай 5 материалов вручную: Project → правый клик → Create → Material, с теми же именами, и назначь их мешам модели.

---

## 8. Шаг 7 — Unity: настройка материалов по одному

Как назначить шейдер: кликни материал в Project → Inspector → вверху выпадающий список **Shader** → набери в поиске **EnergyFlow** → ProjectSphere/EnergyFlow.

### 8.1 Energy - Flow Vertical (энергия в вертикальных трубках)

| Параметр | Значение |
|---|---|
| Shader | ProjectSphere/EnergyFlow |
| Flow Mode | **Vertical Tube** |

Остальное уже выставлено рабочими значениями. Цвета уже в шейдере (Mid Color, Hot Color), Base Color трогать не надо.

### 8.2 Energy - Flow Ring (энергия в горизонтальных кольцах)

| Параметр | Значение |
|---|---|
| Shader | ProjectSphere/EnergyFlow |
| Flow Mode | **Horizontal Ring** |
| Ring Radius | 1.46 (радиус, на котором стоят кольца; см. раздел 10) |

### 8.3 Transparent Glass - Tubes (стекло)

| Параметр | Значение |
|---|---|
| Shader | Universal Render Pipeline/Lit |
| Surface Type (в Surface Inputs) | **Transparent** |
| Rendering Face | **Both** (чтобы стекло было видно изнутри) |
| Base Color | (0.75, 0.88, 1.0) с Alpha ≈ 0.15 |
| Smoothness | 0.95, Metallic 0 |

### 8.4 Graphite Blue - Armor (бронеплиты)

| Параметр | Значение |
|---|---|
| Shader | Universal Render Pipeline/Lit |
| Base Color | (0.12, 0.17, 0.23) |
| Metallic | 0.1 |
| Smoothness | 0.58 |

### 8.5 Inner Body - Dark Blue (центральный корпус)

| Параметр | Значение |
|---|---|
| Shader | Universal Render Pipeline/Lit |
| Base Color | (0.025, 0.045, 0.07) |
| Metallic | 0.15 |
| Smoothness | 0.45 |

Проверка: выбери в Scene любую трубку — в Inspector у меша должен быть правильный материал. Энергия потечёт сразу, даже без Bloom — просто без ореола свечения.

---

## 9. Шаг 8 — Unity: Bloom (ореол свечения)

Без Bloom капли просто ярко-зелёные. Свечение «как в Blender» даёт постобработка.

### 9.1 Включаем постобработку в URP Asset

1. Edit → Project Settings → Graphics → посмотри, какой **URP Asset** назначен (обычно лежит в Assets/Settings).
2. Открой этот asset: поставь галочки **Post Processing** и **HDR**.

### 9.2 Включаем Bloom в сцене

1. В Hierarchy: GameObject → Volume → **Global Volume**.
2. Выбери его → Inspector → **Add Override** → Post Processing → **Bloom**:
   - Threshold ≈ 0.8
   - Intensity ≈ 1.5
3. Выбери Main Camera → Inspector → Rendering → галочка **Post Processing**.

Проверка в Game view: у бегущих капель должен появиться мягкий зелёный ореол.

---

## 10. Если менял размеры в Blender-скрипте

Шейдеру нужно знать два размера, их надо держать синхронно с Blender-скриптом:

| Параметр шейдера | Откуда берётся в скрипте |
|---|---|
| Core Radius (Vertical) | ENERGY_RADIUS (по умолчанию 0.018) |
| Ring Radius (Ring) | примерно DIAMETER / 2 − 0.04 (по умолчанию ≈ 1.46) |

Если поменяешь DIAMETER (диаметр колонны) — обнови Ring Radius в материале Energy - Flow Ring, иначе капли на кольцах будут не того размера.

---

## 11. Параметры шейдера энергии (все в материале)

| Параметр | Что делает | По умолчанию | Рецепты |
|---|---|---|---|
| Flow Speed | скорость течения | 4.0 (~1.4 м/с) | быстрее — 6–8; медленнее — 2 |
| Blob Length | длина капли вдоль трубки, м | 0.35 | крупные капли — 0.5; мелкие частые — 0.2 |
| Blob Contrast | резкость капель | 1.7 | резче и темнее паузы — 2.0–3.0 |
| Min Brightness | свечение пауз | 0 (честные тёмные паузы) | не поднимай выше 0.1 — снова получится «палка» |
| Filaments Around Core | прожилки вокруг стержня | 3 | 2–4 |
| Spark Brightness | быстрые искры | 1.5 | выключить — 0 |
| Spark Period | расстояние между искрами, м | 1.5 | меньше — чаще |
| Overall Brightness | общая яркость | 3 | должна пробивать Threshold Bloom (0.8) |
| Mid Color / Hot Color | цвет капель / горячих сердцевин | зелёный | любой цвет, например оранжевый для «лавы» |

Цвет меняется двумя слайдерами: Mid Color (основной цвет капли) и Hot Color (сердцевина). Deep Color — цвет пауз, обычно чёрный.

---

## 12. Если что-то пошло не так

| Проблема | Причина | Решение |
|---|---|---|
| Трубка — сплошная зелёная палка | Min Brightness больше 0 или Contrast слишком мал | Min Brightness = 0, Contrast = 1.7–2.5 |
| Капли есть, но не светятся ореолом | Bloom не включён | раздел 9: URP Asset (Post Processing, HDR), Global Volume, галочка на камере |
| В Scene светится, в Game — нет | у камеры выключен Post Processing | Main Camera → Rendering → Post Processing ✓ |
| Модель гигантская или крошечная | единицы FBX | вкладка Model → Convert Units вкл/выкл → Apply |
| Все трубки текут синхронно | включён Static Batching | сними галочку Static с трубок; в шейдере уже стоит DisableBatching |
| Трубки чёрные или ярко-розовые | материалу не назначен шейдер / шейдер не в Assets | проверь, что SciFiEnergyColumn.shader лежит в Assets/EnergyColumn и без ошибок |
| FBX не появился | .blend не был сохранён | сохрани .blend и запусти скрипт снова; путь смотри в консоли |
| На кольцах виден шов | Ring Radius сильно отличается от реального радиуса | Ring Radius ≈ DIAMETER / 2 − 0.04 |
| Энергия «плоская», без прожилок | Core Radius не совпадает с радиусом стержня | Core Radius = ENERGY_RADIUS из скрипта (0.018) |
| Скрипт в Blender выдал ошибку | старая версия Blender или открыт диалог | нужен Blender 3.0+; закрой модальные окна и повтори |

---

## 13. Производительность

- Шейдер энергии — **Unlit**: без текстур, без освещения, без теней. Очень дёшев, подходит для мобилок.
- Самое тяжёлое в модели — геометрия (много мелких трубок). Для лёгкого варианта уменьши в Blender-скрипте:
  - CIRCLE_SEGMENTS = 192 → 64 (кольца);
  - VERTICAL_LINES = 16 → 12;
  - HORIZONTAL_ROWS = 14 → 10.
- Стекло с Surface Type = Transparent — недорого при небольшом размере трубок на экране.

---

## 14. Краткая шпаргалка

1. Blender: сохрани .blend → Run Script → FBX появился рядом.
2. Unity: FBX в Assets → Scale 1 → Extract Materials.
3. Energy-материалам — шейдер ProjectSphere/EnergyFlow (Vertical Tube / Horizontal Ring).
4. Стекло — URP/Lit Transparent. Плиты и корпус — URP/Lit.
5. URP Asset: Post Processing + HDR. Global Volume: Bloom. Камера: Post Processing ✓.
6. Трубки не в Static. Скорость — Flow Speed.
