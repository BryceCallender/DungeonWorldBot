// The canvas page's board. Built into wwwroot/canvas.js by `npm run build` (dotnet build runs it too).
import Konva from 'konva';
import { connect, type CanvasItemType, type CanvasLive, type CanvasPerson, type ItemJson, type BoardMove, type MoveAsk, type MoveResult, type Stat, type SavedItem } from './canvas-live';

// ---- The board ----------------------------------------------------------------------------------------------------

// Everything is drawn and saved in these units, whatever size the screen is, so everyone's drawings line up.
// The board goes on forever in every direction. The stage fills the box on the page and is a camera onto it: zooming
// scales it, panning moves it.
// Boards used to be this size, so older drawings sit in it; an empty board opens looking at it.
const HOME = { x: 0, y: 0, width: 1600, height: 1000 };

const container = document.getElementById('container') as HTMLDivElement;
const canvasId = container.dataset.canvasId!;

const stage = new Konva.Stage({ container, width: container.clientWidth, height: container.clientHeight });
const grid = new Konva.Layer({ listening: false });        // squares to line things up on, drawn as you look around
const pictures = new Konva.Layer();                        // maps and handouts, under everything drawn on them
const drawing = new Konva.Layer();                         // saved strokes and text
const tokens = new Konva.Layer();                          // pieces you can move
const handles = new Konva.Layer();                         // the box and corners around selected text
const others = new Konva.Layer({ listening: false });      // other people's cursors and half-drawn strokes
const badges = new Konva.Layer({ listening: false });      // conditions over fighters' tokens, never saved
const effects = new Konva.Layer({ listening: false });     // damage numbers floating off tokens, never saved
stage.add(grid, pictures, drawing, tokens, badges, handles, others, effects);

// The part of the board you can see, in board units
function visibleArea() {
    const scale = stage.scaleX();
    return { x: -stage.x() / scale, y: -stage.y() / scale, width: stage.width() / scale, height: stage.height() / scale };
}

// A square every 50 units, drawn only where you're looking, since there's no end to them. Gray at low opacity reads
// on both the light and dark board. Lines stay 1px however far you zoom, and zoomed far out only every fifth one is
// drawn, so the board doesn't turn into gray mush. Taking the thumbnail points it at the part being pictured instead.
const GRID_SIZE = 50;
const surface = getComputedStyle(container).getPropertyValue('--surface').trim() || '#ffffff';
let gridArea: { x: number; y: number; width: number; height: number } | null = null;
grid.add(new Konva.Shape({
    sceneFunc: context => {
        const area = gridArea ?? visibleArea();
        const scale = stage.scaleX();
        context.fillStyle = surface;
        context.fillRect(area.x, area.y, area.width, area.height);

        const step = GRID_SIZE * scale < 10 ? GRID_SIZE * 5 : GRID_SIZE;
        context.beginPath();
        for (let x = Math.ceil(area.x / step) * step; x <= area.x + area.width; x += step) {
            context.moveTo(x, area.y);
            context.lineTo(x, area.y + area.height);
        }
        for (let y = Math.ceil(area.y / step) * step; y <= area.y + area.height; y += step) {
            context.moveTo(area.x, y);
            context.lineTo(area.x + area.width, y);
        }
        context.strokeStyle = 'rgba(128,128,128,0.18)';
        context.lineWidth = 1 / scale;
        context.stroke();
    },
}));

// ---- Pan and zoom --------------------------------------------------------------------------------------------------

// Until you pan or zoom, the view keeps fitting what's drawn as the window resizes and the board loads
let viewMoved = false;

// 100% is one board unit to a pixel
const MIN_ZOOM = 0.05;
const MAX_ZOOM = 5;

function resize() {
    // A box with no size yet (hidden, or still laying out) would make the scale 0; wait until it has one
    if (!container.clientWidth || !container.clientHeight) return;
    stage.size({ width: container.clientWidth, height: container.clientHeight });
    if (!viewMoved) fitView();
}

// Everything drawn, in board units, or null when nothing is
function contentBounds() {
    let left = Infinity, top = Infinity, right = -Infinity, bottom = -Infinity;
    for (const layer of [pictures, drawing, tokens])
        for (const item of layer.getChildren()) {
            if (!item.visible()) continue;
            const r = item.getClientRect({ relativeTo: stage });   // in board units, whatever the zoom
            left = Math.min(left, r.x); top = Math.min(top, r.y);
            right = Math.max(right, r.x + r.width); bottom = Math.max(bottom, r.y + r.height);
        }
    return left === Infinity ? null : { x: left, y: top, width: right - left, height: bottom - top };
}

// `box` (in board units) grown to at least `least` wide and in `aspect`'s shape, around the same middle
function frameAround(box: { x: number; y: number; width: number; height: number }, padding: number, least: number, aspect: number) {
    let width = Math.max(box.width + padding * 2, least);
    let height = Math.max(box.height + padding * 2, width / aspect);
    width = Math.max(width, height * aspect);
    height = width / aspect;
    return { x: box.x + box.width / 2 - width / 2, y: box.y + box.height / 2 - height / 2, width, height };
}

// Everything that's drawn, or where boards start when nothing is. One scribble isn't blown up to fill the screen.
function fitView() {
    const content = contentBounds();
    const frame = content ? frameAround(content, 40, HOME.width / 2, stage.width() / stage.height()) : HOME;
    const scale = clampZoom(Math.min(stage.width() / frame.width, stage.height() / frame.height));
    stage.scale({ x: scale, y: scale });
    stage.position({
        x: stage.width() / 2 - (frame.x + frame.width / 2) * scale,
        y: stage.height() / 2 - (frame.y + frame.height / 2) * scale,
    });
    viewMoved = false;
}

const clampZoom = (scale: number) => Math.min(Math.max(scale, MIN_ZOOM), MAX_ZOOM);

// Zoom by `factor`, keeping the board point under `screen` (a spot in the box, in pixels) where it is
function zoomAt(screen: { x: number; y: number }, factor: number) {
    const old = stage.scaleX();
    const next = clampZoom(old * factor);
    const board = { x: (screen.x - stage.x()) / old, y: (screen.y - stage.y()) / old };
    stage.scale({ x: next, y: next });
    stage.position({ x: screen.x - board.x * next, y: screen.y - board.y * next });
    viewMoved = true;
}

// Puts a board point in the middle of the box, keeping the zoom
function lookAt(point: { x: number; y: number }) {
    stage.position({ x: stage.width() / 2 - point.x * stage.scaleX(), y: stage.height() / 2 - point.y * stage.scaleY() });
    viewMoved = true;
}

const zoomLevel = document.querySelector<HTMLElement>('[data-zoom="fit"]');
stage.on('scaleXChange', () => {
    if (zoomLevel) zoomLevel.textContent = `${Math.round(stage.scaleX() * 100)}%`;
});

// Trackpads: two fingers pan, pinching zooms (the browser reports a pinch as a wheel with ctrlKey).
// Mice: the wheel pans up and down, Shift+wheel sideways, Ctrl/Cmd+wheel zooms.
stage.on('wheel', e => {
    e.evt.preventDefault();
    if (e.evt.ctrlKey || e.evt.metaKey) {
        // Clamped so one notch of a mouse wheel is a step, not a leap
        const delta = Math.max(-50, Math.min(50, e.evt.deltaY));
        zoomAt(stage.getPointerPosition()!, Math.exp(-delta * 0.01));
    } else {
        const dx = e.evt.shiftKey && !e.evt.deltaX ? e.evt.deltaY : e.evt.deltaX;
        const dy = e.evt.shiftKey && !e.evt.deltaX ? 0 : e.evt.deltaY;
        stage.position({ x: stage.x() - dx, y: stage.y() - dy });
        viewMoved = true;
    }
});

// Dragging the board itself pans: with the Move tool on empty space, or with any tool while Space is held.
// A token under the pointer wins, since Konva drags the deepest draggable thing.
let spaceHeld = false;
function updatePanning() {
    stage.draggable(tool === 'move' || spaceHeld);
    container.classList.toggle('panning', spaceHeld);
}
stage.on('dragmove', e => { if (e.target === stage) viewMoved = true; });

new ResizeObserver(resize).observe(container);
resize();

// Where the pointer is, in board units rather than screen pixels, to a tenth (more is just bigger JSON)
function pointer() {
    const { x, y } = stage.getRelativePointerPosition() ?? { x: 0, y: 0 };
    return { x: Math.round(x * 10) / 10, y: Math.round(y * 10) / 10 };
}

// ---- Items: turning saved JSON into shapes and back ----------------------------------------------------------------

const layerFor = (type: CanvasItemType) => (type === 'token' ? tokens : type === 'image' ? pictures : drawing);

// The node an item is saved as: the shape itself, or the group a token's circle and label sit in
function itemOf(shape: Konva.Node): Konva.Node | null {
    let node: Konva.Node | null = shape;
    while (node && !(node.getParent() instanceof Konva.Layer)) node = node.getParent();
    return node && node.getLayer() !== others ? node : null;
}

// Things the saved JSON doesn't say but the page needs, like how easy a thin line is to hit with the eraser
function prepare(node: Konva.Node, type: CanvasItemType) {
    if (node instanceof Konva.Line) node.hitStrokeWidth(20);
    if (type === 'shape' && node instanceof Konva.Shape) node.hitStrokeWidth(20);
    if (type === 'token' || type === 'image' || type === 'shape' || node instanceof Konva.Text) node.draggable(canDrag(node));
    showPictures(node);
}

// ---- Pictures ------------------------------------------------------------------------------------------------------

// A loaded picture can't be saved as JSON, so items keep its URL in a `src` attribute and get the picture put back
// here. Each URL is fetched once, however many tokens use it.
const loading = new Map<string, Promise<HTMLImageElement>>();
function loadPicture(src: string) {
    let picture = loading.get(src);
    if (!picture) {
        picture = new Promise((resolve, reject) => {
            const img = new Image();
            img.onload = () => resolve(img);
            img.onerror = reject;
            img.src = src;
        });
        loading.set(src, picture);
    }
    return picture;
}

// The picture URL a node keeps, if it has one
const srcOf = (node: Konva.Node): string | undefined => node.getAttr('src');

// A map locked from its right-click menu stays put: it can't be dragged, scaled or deleted until it's unlocked, so
// dragging on it pans the board instead. The `locked` attribute is saved with it, so it's locked for everyone.
const lockedInPlace = (node: Konva.Node) => node.getAttr('locked') === true;
const canDrag = (node: Konva.Node) => tool === 'move' && !viewOnly && !lockedInPlace(node);

// The node, and anything in it, with a picture to show: a board image, or the circle of an image token
function showPictures(node: Konva.Node) {
    const all: Konva.Node[] = node instanceof Konva.Container ? [node, ...node.find((n: Konva.Node) => !!srcOf(n))] : [node];
    for (const target of all) {
        const src = srcOf(target);
        if (!src) continue;
        loadPicture(src).then(img => {
            if (target instanceof Konva.Image) {
                target.image(img);
            } else if (target instanceof Konva.Circle) {
                // Fill the circle with the middle of the picture, however wide or tall it is
                const scale = (target.radius() * 2) / Math.min(img.width, img.height);
                target.fillPatternImage(img);
                target.fillPriority('pattern');            // a shape with a color and a pattern shows the color otherwise
                target.fillPatternRepeat('no-repeat');
                target.fillPatternScale({ x: scale, y: scale });
                target.fillPatternOffset({ x: img.width / 2, y: img.height / 2 });
            }
        }).catch(() => { /* a picture that's gone leaves the shape plain */ });
    }
}

// Tokens the fight put on the board for its enemies (made by EnemyTokens on the server, which signs them with this).
// Only the DM can remove them, so a player's stray click can't take an enemy away; the server refuses it too.
const FIGHT_AUTHOR = 'encounter';
const runsFights = container.dataset.runsFights === 'true';
const locked = new Set<string>();
const isLocked = (node: Konva.Node) => !runsFights && locked.has(node.id());

// Whether you can only look at the board, not change it: someone watching a fight they're not in (see checkViewOnly)
let viewOnly = false;

// Each fighter's condition badge, by their token (see showBadges)
const badgeFor = new Map<Konva.Node, Konva.Label>();

function addItem(id: string, type: CanvasItemType, json: ItemJson, author?: string) {
    if (author === FIGHT_AUTHOR) locked.add(id);
    const wasSelected = selected()?.id() === id;
    stage.findOne('#' + id)?.destroy();                    // the same ID again replaces it
    let node = Konva.Node.create(json);
    if (type === 'token') node = withInitials(node);
    node.id(id);
    prepare(node, type);
    layerFor(type).add(node as Konva.Shape | Konva.Group);
    if (wasSelected) select(node);
    if (type === 'token') listTokens();
    return node;
}

function removeItem(node: Konva.Node) {
    locked.delete(node.id());
    if (selected() === node) select(null);
    const wasToken = node.getLayer() === tokens;
    node.destroy();
    if (wasToken) listTokens();
}

function load(items: SavedItem[]) {
    drawing.destroyChildren();
    tokens.destroyChildren();
    locked.clear();
    for (const item of items) addItem(item.id, item.type, item.item, item.authorID);
    listTokens();
}

// ---- The token list beside the board -------------------------------------------------------------------------------

const tokenList = document.querySelector<HTMLOListElement>('[data-tokens]')!;

// A token is a circle (like the sample ones) or a group with a circle and its initials in it
function tokenColor(token: Konva.Node) {
    const circle = token instanceof Konva.Circle ? token : (token as Konva.Group).findOne<Konva.Circle>('Circle');
    return (circle?.fill() as string | undefined) ?? '#888780';
}

function listTokens() {
    const all = tokens.getChildren();
    document.querySelector<HTMLElement>('[data-tokens-empty]')!.hidden = all.length > 0;
    tokenList.replaceChildren(...all.map(token => {
        const button = document.createElement('button');
        button.type = 'button';
        button.dataset.token = token.id();
        const dot = Object.assign(document.createElement('span'), { className: 'dot' });
        dot.style.background = tokenColor(token);
        const holder = token instanceof Konva.Group ? token.findOne((n: Konva.Node) => !!srcOf(n)) : token;
        const picture = holder && srcOf(holder);
        if (picture) dot.style.background = `center / cover url("${picture}")`;
        button.append(dot, Object.assign(document.createElement('span'), { className: 'name', textContent: token.name() || 'Token' }));
        const li = document.createElement('li');
        li.append(button);
        // An enemy's token stays until its fight is done with it, and nothing's yours to remove while you only watch
        if (isLocked(token) || viewOnly) return li;
        // Beside the name rather than in it, since a button can't hold another button
        const remove = Object.assign(document.createElement('button'), {
            type: 'button', className: 'remove', textContent: '×', title: `Remove ${token.name() || 'token'}`,
        });
        remove.dataset.removeToken = token.id();
        remove.setAttribute('aria-label', remove.title);
        li.append(remove);
        return li;
    }));
    showDefeated();
    // Tokens arrive after the panel, and come and go, so their badges are worked out again
    showBadges();
}

// ---- Defeated enemies ----------------------------------------------------------------------------------------------

// An enemy beaten down to 0 HP goes gray on the board and in the list. The encounter panel marks who's defeated, and
// app.js swaps in a fresh one whenever the bot changes something, so the board looks again each time (see checkViewOnly).
// Tokens placed by the fight know their enemy's ID; ones made by hand go by their name.
function defeatedEnemies() {
    const rows = [...document.querySelectorAll<HTMLElement>('[data-encounter-panel] .enemy.defeated')];
    return {
        ids: new Set(rows.map(r => r.dataset.enemyId)),
        names: new Set(rows.map(r => r.dataset.enemy?.toLowerCase())),
    };
}

function isDefeated(token: Konva.Node, defeated = defeatedEnemies()) {
    const [kind, id] = fighterOf(token);
    if (kind !== undefined) return kind === 'enemy' && defeated.ids.has(id);
    return !!token.name() && defeated.names.has(token.name().toLowerCase());
}

// Grayed as it's drawn rather than with a Konva filter, since filters are attributes and would be saved with the token
type SceneCanvas = Parameters<Konva.Group['drawScene']>[0];
const drawGroup = Konva.Group.prototype.drawScene;
const drawCircle = Konva.Circle.prototype.drawScene;
function drawGray(this: Konva.Node, draw: Function, canvas: SceneCanvas, ...rest: unknown[]) {
    const context = (canvas ?? this.getLayer()?.getCanvas())?.getContext() as Konva.Context | undefined;
    context?.save();
    context?.setAttr('filter', 'grayscale(1) opacity(.55)');
    draw.call(this, canvas, ...rest);
    context?.restore();
    return this;
}

// Kept here rather than as an attribute, which would be saved with the token too
const grayed = new WeakSet<Konva.Node>();

function showDefeated() {
    const defeated = defeatedEnemies();
    for (const token of tokens.getChildren()) {
        const gray = isDefeated(token, defeated);
        if (gray === grayed.has(token)) continue;
        if (gray) grayed.add(token); else grayed.delete(token);
        const draw = token instanceof Konva.Group ? drawGroup : drawCircle;
        (token as Konva.Group).drawScene = gray
            ? function (this: Konva.Node, canvas?: SceneCanvas, ...rest: unknown[]) { return drawGray.call(this, draw, canvas, ...rest); } as Konva.Group['drawScene']
            : draw as Konva.Group['drawScene'];
    }
    tokens.batchDraw();
    for (const li of tokenList.children) {
        const id = li.querySelector<HTMLElement>('[data-token]')?.dataset.token;
        const token = id && stage.findOne('#' + id);
        li.classList.toggle('defeated', !!token && isDefeated(token, defeated));
    }
}

// Clicking a token in the list makes it jump, so you can find it on a busy board
// and its × takes it off the board for everyone
tokenList.addEventListener('click', e => {
    const removeId = (e.target as HTMLElement).closest<HTMLElement>('[data-remove-token]')?.dataset.removeToken;
    const removing = removeId && stage.findOne('#' + removeId);
    if (removing && !isLocked(removing) && !viewOnly) {
        removeMine(removing);
        return;
    }

    const id = (e.target as HTMLElement).closest<HTMLElement>('[data-token]')?.dataset.token;
    const token = id && stage.findOne('#' + id);
    if (!token) return;
    // From whatever size it's been made, and back to it. Clicking again mid-pulse starts over from that size, not from
    // however big the pulse had got, or quick clicks would leave it bigger each time.
    // (A tween that's replaced still runs out and calls its onFinish, so the one before is stopped outright.)
    const before = pulsing.get(token);
    before?.tween.destroy();
    const size = before?.size ?? token.scaleX();
    const tween = (scale: number, then: () => void) => {
        const t: Konva.Tween = new Konva.Tween({
            node: token, scaleX: scale, scaleY: scale, duration: 0.15, onFinish: () => { t.destroy(); then(); },
        });
        pulsing.set(token, { size, tween: t });
        t.play();
    };
    tween(size * 1.6, () => tween(size, () => pulsing.delete(token)));
});

// Tokens partway through a pulse, the size each goes back to, and the tween running now
const pulsing = new Map<Konva.Node, { size: number; tween: Konva.Tween }>();

// ---- Tools ---------------------------------------------------------------------------------------------------------

type Tool = 'pen' | 'erase' | 'move' | 'token' | 'text' | 'shape';
type ShapeKind = 'rect' | 'ellipse' | 'line' | 'arrow';
let shapeKind: ShapeKind = 'rect';
let tool: Tool = 'move';
let color = (document.querySelector<HTMLElement>('[data-color].on')?.dataset.color) ?? '#7F77DD';
const widthButtons = [...document.querySelectorAll<HTMLElement>('[data-width]')];
let penWidth = Number(document.querySelector<HTMLElement>('[data-width].on')?.dataset.width ?? 16);

// The strip of shape kinds beside the Shape button, which opens like a menu and closes once you've picked
const shapeTool = document.querySelector<HTMLElement>('.shape-tool')!;
const showShapes = (open: boolean) => shapeTool.classList.toggle('open', open);

function setTool(next: Tool) {
    // Watching, Move is the only tool, for panning
    if (viewOnly && next !== 'move') return;
    if (next !== 'shape') showShapes(false);
    tool = next;
    container.dataset.tool = next;                         // for the cursor, in ScenePage.razor.css
    document.querySelectorAll<HTMLElement>('.board-rail [data-tool]').forEach(b => b.classList.toggle('on', b.dataset.tool === next));
    tokens.getChildren().forEach(t => t.draggable(canDrag(t)));
    drawing.find('Text, .shape').forEach(t => t.draggable(canDrag(t)));
    pictures.getChildren().forEach(p => p.draggable(canDrag(p)));
    if (next !== 'move') select(null);
    updatePanning();
}

function pickColor(next: string, swatch: Element) {
    color = next;
    document.querySelectorAll('.swatch, .color-picker').forEach(b => b.classList.toggle('on', b === swatch));
    // Picking a color means you want to draw with it, unless you're placing tokens, text or shapes in it
    if (tool !== 'token' && tool !== 'text' && tool !== 'shape') setTool('pen');
}

// The thickness buttons, each a dot of its size
function pickWidth(button: HTMLElement) {
    penWidth = Number(button.dataset.width);
    widthButtons.forEach(b => b.classList.toggle('on', b === button));
    // Like a color: you want to draw with it, unless you're about to drag out a shape
    if (tool !== 'shape') setTool('pen');
}

// [ and ] step to the next thinner or thicker
function stepWidth(by: number) {
    const now = widthButtons.findIndex(b => b.classList.contains('on'));
    const next = widthButtons[Math.max(0, Math.min(widthButtons.length - 1, now + by))];
    if (next) pickWidth(next);
}

// The palette button: the browser's own color picker, which fires as you drag around in it
const customColor = document.querySelector<HTMLInputElement>('[data-custom-color]')!;
customColor.addEventListener('input', () => {
    const swatch = customColor.closest<HTMLElement>('.color-picker')!;
    swatch.style.setProperty('--swatch', customColor.value);
    pickColor(customColor.value, swatch);
});
// Clicking it again once it has a color uses that color, as well as opening the picker
customColor.addEventListener('click', () => {
    const swatch = customColor.closest<HTMLElement>('.color-picker')!;
    if (swatch.style.getPropertyValue('--swatch')) pickColor(customColor.value, swatch);
});

// The tool rail, and the sidebar's "Add token"
document.querySelector('.board-page')!.addEventListener('click', e => {
    const button = (e.target as HTMLElement).closest('button');
    if (!button) return;
    // The Shape button opens its strip; clicking it again while it's open closes it
    if (button.dataset.tool === 'shape') showShapes(!(tool === 'shape' && shapeTool.classList.contains('open')));
    if (button.dataset.tool) setTool(button.dataset.tool as Tool);
    if (button.dataset.color) pickColor(button.dataset.color, button);
    if (button.dataset.width) pickWidth(button);
    if (button.hasAttribute('data-undo')) undo();
    if (button.dataset.shape) {
        shapeKind = button.dataset.shape as ShapeKind;
        document.querySelectorAll('[data-shape]').forEach(b => b.classList.toggle('on', b === button));
        setTool('shape');
        showShapes(false);
    }
    // The buttons zoom around the middle of the box
    const middle = { x: stage.width() / 2, y: stage.height() / 2 };
    if (button.dataset.zoom === 'in') zoomAt(middle, 1.25);
    if (button.dataset.zoom === 'out') zoomAt(middle, 0.8);
    if (button.dataset.zoom === 'fit') fitView();
});

const keys: Record<string, Tool> = { p: 'pen', e: 'erase', m: 'move', x: 'text', s: 'shape' };
document.addEventListener('keydown', e => {
    // Typing text on the board, or anywhere else, isn't a shortcut; nor is anything in an open dialog
    if ((e.target as HTMLElement).closest('input, textarea, select, [contenteditable], dialog')) return;
    if ((e.ctrlKey || e.metaKey) && e.key === 'z') { e.preventDefault(); undo(); return; }
    if ((e.ctrlKey || e.metaKey) && (e.key === '=' || e.key === '+')) { e.preventDefault(); zoomAt({ x: stage.width() / 2, y: stage.height() / 2 }, 1.25); return; }
    if ((e.ctrlKey || e.metaKey) && e.key === '-') { e.preventDefault(); zoomAt({ x: stage.width() / 2, y: stage.height() / 2 }, 0.8); return; }
    if ((e.ctrlKey || e.metaKey) && e.key === '0') { e.preventDefault(); fitView(); return; }
    if (e.key === ' ' && !spaceHeld) { e.preventDefault(); spaceHeld = true; updatePanning(); return; }
    if ((e.key === '[' || e.key === ']') && !e.ctrlKey && !e.metaKey && !viewOnly) { stepWidth(e.key === '[' ? -1 : 1); return; }
    const chosen = selected();
    if (chosen && (e.key === 'Delete' || e.key === 'Backspace')) { if (!isLocked(chosen) && !viewOnly) removeMine(chosen); return; }
    if (!e.ctrlKey && !e.metaKey && !e.altKey && keys[e.key]) setTool(keys[e.key]);
});
document.addEventListener('keyup', e => {
    if (e.key === ' ') { spaceHeld = false; updatePanning(); }
});

// Like a menu: pressing anywhere else (the board included) or Escape closes the shape strip
document.addEventListener('pointerdown', e => {
    if (!(e.target as HTMLElement).closest('.shape-tool')) showShapes(false);
});
document.addEventListener('keydown', e => { if (e.key === 'Escape') showShapes(false); });

// What you've changed, newest last, for undo: each item as it was before, so undoing puts it back that way. Something
// you added has no before, so undoing takes it away.
interface Change {
    kind: 'added' | 'changed' | 'removed';
    id: string;
    type: CanvasItemType;
    before: ItemJson | null;
    locked: boolean;
}
const history: Change[] = [];

const typeOf = (node: Konva.Node): CanvasItemType =>
    node.getLayer() === tokens ? 'token'
        : node.getLayer() === pictures ? 'image'
        : node instanceof Konva.Text ? 'text'
        : node.hasName('shape') ? 'shape'
        : 'stroke';

// A copy of the item as it's saved, which later changes to the node can't reach
const snapshot = (node: Konva.Node) => JSON.parse(node.toJSON()) as ItemJson;

function remember(kind: Change['kind'], node: Konva.Node, before: ItemJson | null = kind === 'added' ? null : snapshot(node)) {
    history.push({ kind, id: node.id(), type: typeOf(node), before, locked: locked.has(node.id()) });
}

// Taking something off the board yourself, which undo can put back
function removeMine(node: Konva.Node) {
    remember('removed', node);
    live?.removeItem(node.id());
    removeItem(node);
}

function undo() {
    if (viewOnly) return;
    while (history.length) {
        const { kind, id, type, before, locked: wasLocked } = history.pop()!;
        const node = stage.findOne('#' + id);
        // Someone else already erased it, so there's nothing to take away or change back
        if (kind !== 'removed' && !node) continue;
        if (kind === 'added') {
            live?.removeItem(id);
            removeItem(node!);
        } else {
            addItem(id, type, before!, wasLocked ? FIGHT_AUTHOR : undefined);
            live?.addItem(id, type, before!);
        }
        return;
    }
}

// ---- Drawing -------------------------------------------------------------------------------------------------------

// The stroke being drawn, and the points not sent to the others yet
let stroke: { line: Konva.Line; unsent: number[]; timer: number | null } | null = null;
let erasing = false;

stage.on('pointerdown', e => {
    if (spaceHeld || viewOnly || aiming) return;           // the stage drag pans instead; aiming picks a target
    if (e.evt.button !== 0) return;                        // a right-click is for the picture menu, not drawing
    const { x, y } = pointer();
    if (tool === 'pen') {
        const line = new Konva.Line({
            id: crypto.randomUUID(),
            points: [x, y],
            stroke: color,
            strokeWidth: penWidth,
            lineCap: 'round',
            lineJoin: 'round',
            tension: 0.5,
            hitStrokeWidth: 20,
        });
        drawing.add(line);
        stroke = { line, unsent: [x, y], timer: null };
    } else if (tool === 'move') {
        // Text, shapes, pictures and tokens under the pointer get selection handles; anywhere else drops them.
        // Handles themselves are left alone.
        if (e.target.getParent() instanceof Konva.Transformer) return;
        const onDrawing = e.target.getLayer() === drawing;
        const text = e.target instanceof Konva.Text && onDrawing;
        const shape = e.target.hasName('shape') && onDrawing;
        const picture = e.target instanceof Konva.Image && e.target.getLayer() === pictures && !lockedInPlace(e.target);
        // A token is a group, so whichever part was clicked (its circle or initials), the whole token is selected
        const token = e.target.getLayer() === tokens ? itemOf(e.target) : null;
        select(token ?? (text || shape || picture ? e.target : null));
    } else if (tool === 'erase') {
        erasing = true;
        eraseHere();
    } else if (tool === 'token') {
        placeToken(x, y);
    } else if (tool === 'shape') {
        shapeDraft = { node: newShape(shapeKind, x, y), start: { x, y } };
        drawing.add(shapeDraft.node);
    } else if (tool === 'text') {
        // Clicking existing text edits it; clicking anywhere else starts new text there
        const target = e.target instanceof Konva.Text && e.target.getLayer() === drawing ? e.target : null;
        editText(target, target ? target.position() : { x, y });
    }
});

stage.on('pointermove', () => {
    const { x, y } = pointer();
    live?.cursor(x, y);

    if (stroke) {
        const points = stroke.line.points();
        // Skip points too close to the last one; they add size, not shape
        if (Math.hypot(x - points[points.length - 2], y - points[points.length - 1]) < 2) return;
        stroke.line.points(points.concat(x, y));
        stroke.unsent.push(x, y);
        // Send the new points in batches, about 20 a second, so the others see it being drawn
        stroke.timer ??= window.setTimeout(sendProgress, 50);
    } else if (shapeDraft) {
        sizeShape(shapeDraft.node, shapeDraft.start, { x, y }, shiftHeld);
    } else if (erasing) {
        eraseHere();
    }
});

function sendProgress() {
    if (!stroke) return;
    stroke.timer = null;
    if (stroke.unsent.length) live?.strokeProgress(stroke.line.id(), stroke.unsent.splice(0));
}

// On the window, so letting go outside the board still finishes the stroke
window.addEventListener('pointerup', () => {
    erasing = false;
    if (shapeDraft) finishShape();
    if (!stroke) return;
    if (stroke.timer) clearTimeout(stroke.timer);
    sendProgress();
    const { line } = stroke;
    stroke = null;
    live?.strokeEnd(line.id(), line.toObject());
    remember('added', line);
});

function eraseHere() {
    const shape = stage.getIntersection(stage.getPointerPosition()!);
    const item = shape && itemOf(shape);
    // Sweeping the eraser over a map shouldn't take the whole map; pictures go with Move, then Delete
    if (!item || item.getLayer() === pictures || isLocked(item)) return;
    removeMine(item);
}

// ---- Shapes --------------------------------------------------------------------------------------------------------

// The shape being dragged out, from where it started
let shapeDraft: { node: Konva.Shape; start: { x: number; y: number } } | null = null;

// Shift makes squares, circles and lines at 45° steps
let shiftHeld = false;
document.addEventListener('keydown', e => { if (e.key === 'Shift') shiftHeld = true; });
document.addEventListener('keyup', e => { if (e.key === 'Shift') shiftHeld = false; });

function newShape(kind: ShapeKind, x: number, y: number): Konva.Shape {
    // Named 'shape' so Move can tell them from freehand strokes. Lines keep their width however they're resized.
    const look = {
        id: crypto.randomUUID(), name: 'shape', x, y, stroke: color, strokeWidth: penWidth,
        strokeScaleEnabled: false, hitStrokeWidth: 20, lineCap: 'round' as const, lineJoin: 'round' as const,
    };
    switch (kind) {
        case 'rect': return new Konva.Rect({ ...look, width: 0, height: 0, cornerRadius: 4 });
        case 'ellipse': return new Konva.Ellipse({ ...look, radiusX: 0, radiusY: 0 });
        case 'line': return new Konva.Line({ ...look, points: [0, 0, 0, 0] });
        case 'arrow': return new Konva.Arrow({ ...look, points: [0, 0, 0, 0], fill: color, pointerLength: Math.max(14, penWidth * 3), pointerWidth: Math.max(14, penWidth * 3) });
    }
}

// Stretch it from where the drag started to where the pointer is
function sizeShape(node: Konva.Shape, start: { x: number; y: number }, at: { x: number; y: number }, even: boolean) {
    let dx = at.x - start.x, dy = at.y - start.y;
    if (node instanceof Konva.Line) {                     // a line or arrow, drawn from its start
        if (even) {
            const angle = Math.round(Math.atan2(dy, dx) / (Math.PI / 4)) * (Math.PI / 4);
            const length = Math.hypot(dx, dy);
            dx = Math.round(Math.cos(angle) * length);
            dy = Math.round(Math.sin(angle) * length);
        }
        node.points([0, 0, dx, dy]);
        return;
    }
    if (even) {
        const side = Math.max(Math.abs(dx), Math.abs(dy));
        dx = Math.sign(dx || 1) * side;
        dy = Math.sign(dy || 1) * side;
    }
    if (node instanceof Konva.Ellipse) {                  // placed by its middle
        node.position({ x: start.x + dx / 2, y: start.y + dy / 2 });
        node.radius({ x: Math.abs(dx) / 2, y: Math.abs(dy) / 2 });
    } else {                                              // a rectangle, placed by its top-left corner
        node.position({ x: Math.min(start.x, start.x + dx), y: Math.min(start.y, start.y + dy) });
        node.size({ width: Math.abs(dx), height: Math.abs(dy) });
    }
}

function finishShape() {
    const { node } = shapeDraft!;
    shapeDraft = null;
    // A click without a drag isn't a shape
    const box = node.getClientRect({ skipStroke: true, relativeTo: stage });
    if (Math.max(box.width, box.height) < 4) { node.destroy(); return; }
    live?.addItem(node.id(), 'shape', node.toObject());
    remember('added', node);
}

// ---- Moving and scaling text, shapes and pictures --------------------------------------------------------------------------------------

// Konva's Transformer draws a box with corner handles around whatever it's given. Dragging a corner scales the text,
// picture or token; dragging it moves it (they're draggable with the Move tool).
const transformer = new Konva.Transformer({
    keepRatio: true,
    rotateEnabled: false,
    enabledAnchors: ['top-left', 'top-right', 'bottom-left', 'bottom-right'],
    anchorSize: 9,
    anchorCornerRadius: 2,
    borderStroke: '#7F77DD',
    anchorStroke: '#7F77DD',
    // Not smaller than readable
    boundBoxFunc: (old, next) => (next.height < 10 ? old : next),
});
handles.add(transformer);

const selected = () => transformer.nodes()[0] ?? null;
const isToken = (node: Konva.Node) => node.getLayer() === tokens;
function select(shape: Konva.Node | null) {
    // Text, pictures and tokens keep their shape; drawn shapes can be stretched either way. (A token's name is what
    // it's called, which could be anything, even "shape".)
    const free = !!shape && !isToken(shape) && shape.hasName('shape');
    transformer.keepRatio(!free);
    transformer.enabledAnchors(free
        ? ['top-left', 'top-center', 'top-right', 'middle-left', 'middle-right', 'bottom-left', 'bottom-center', 'bottom-right']
        : ['top-left', 'top-right', 'bottom-left', 'bottom-right']);
    transformer.nodes(shape ? [shape] : []);
}

// The handles scale it; when you let go, the scale becomes a real font size or width and height, so text stays crisp
// and what's saved is plain bigger (or smaller) text or picture
// How it was before the handles were dragged, for undo
let beforeTransform: ItemJson | null = null;
transformer.on('transformstart', () => { const shape = selected(); beforeTransform = shape && snapshot(shape); });

transformer.on('transformend', () => {
    const shape = selected();
    if (!shape) return;
    if (beforeTransform) remember('changed', shape, beforeTransform);
    beforeTransform = null;
    if (isToken(shape)) {
        // Tokens keep their scale too: a token is a circle with initials or a picture in it, all of which grow with it
        shape.scale({ x: Math.round(shape.scaleX() * 100) / 100, y: Math.round(shape.scaleX() * 100) / 100 });
        shape.x(Math.round(shape.x() * 10) / 10);
        shape.y(Math.round(shape.y() * 10) / 10);
        live?.addItem(shape.id(), 'token', shape.toObject());
        return;
    }
    if (shape.hasName('shape')) {
        // Shapes keep their scale: their lines don't thicken (strokeScaleEnabled is off), so there's nothing to convert
        shape.x(Math.round(shape.x() * 10) / 10);
        shape.y(Math.round(shape.y() * 10) / 10);
        live?.addItem(shape.id(), 'shape', shape.toObject());
        return;
    }
    if (shape instanceof Konva.Text) {
        shape.fontSize(Math.max(8, Math.round(shape.fontSize() * shape.scaleX())));
    } else {
        shape.width(Math.round(shape.width() * shape.scaleX()));
        shape.height(Math.round(shape.height() * shape.scaleY()));
    }
    shape.scale({ x: 1, y: 1 });
    shape.x(Math.round(shape.x() * 10) / 10);
    shape.y(Math.round(shape.y() * 10) / 10);
    live?.addItem(shape.id(), shape instanceof Konva.Text ? 'text' : 'image', shape.toObject());
});

// Double-click text with the Move tool to change the words
drawing.on('dblclick dbltap', e => {
    if (tool === 'move' && !viewOnly && e.target instanceof Konva.Text) {
        select(null);
        editText(e.target, e.target.position());
    }
});

// ---- Right-click menu on pictures ----------------------------------------------------------------------------------

// Right-clicking a picture, even under strokes or tokens drawn on it, offers to lock it in place or unlock it.
// Anywhere else, the browser's own menu shows as usual.
const pictureMenu = document.createElement('div');
pictureMenu.className = 'board-menu';
pictureMenu.hidden = true;
pictureMenu.setAttribute('role', 'menu');
const lockButton = Object.assign(document.createElement('button'), { type: 'button' });
lockButton.setAttribute('role', 'menuitem');
pictureMenu.append(lockButton);
container.append(pictureMenu);

let menuFor: Konva.Node | null = null;

function showPictureMenu(picture: Konva.Node, at: { x: number; y: number }) {
    menuFor = picture;
    lockButton.textContent = lockedInPlace(picture) ? 'Unlock picture' : 'Lock picture';
    pictureMenu.hidden = false;
    // Kept inside the board, so one opened near the edge isn't cut off
    const box = container.getBoundingClientRect();
    pictureMenu.style.left = `${Math.min(at.x - box.left, box.width - pictureMenu.offsetWidth - 4)}px`;
    pictureMenu.style.top = `${Math.min(at.y - box.top, box.height - pictureMenu.offsetHeight - 4)}px`;
    lockButton.focus();
}

function hidePictureMenu() {
    pictureMenu.hidden = true;
    menuFor = null;
}

stage.on('contextmenu', e => {
    hidePictureMenu();
    hideMoveMenu();
    if (viewOnly) return;
    const at = { x: e.evt.clientX, y: e.evt.clientY };
    const picture = pictures.getIntersection(stage.getPointerPosition()!);
    // A token in a fight offers its moves instead, once the server says which. With none, it's the picture under it.
    const token = e.target.getLayer() === tokens ? itemOf(e.target) : null;
    if (token && mightHaveMoves(token)) {
        e.evt.preventDefault();
        void showMoveMenu(token, at).then(shown => { if (!shown && picture) showPictureMenu(picture, at); });
        return;
    }
    if (!picture) return;
    e.evt.preventDefault();
    showPictureMenu(picture, at);
});

lockButton.addEventListener('click', () => {
    const picture = menuFor;
    hidePictureMenu();
    if (!picture || viewOnly) return;
    remember('changed', picture);
    if (lockedInPlace(picture)) picture.setAttr('locked', undefined);
    else picture.setAttr('locked', true);
    picture.draggable(canDrag(picture));
    if (selected() === picture) select(null);
    live?.addItem(picture.id(), 'image', picture.toObject());   // the same ID replaces it for everyone
});

// Like any menu: pressing anywhere else, Escape, or moving the board closes it
document.addEventListener('pointerdown', e => { if (!pictureMenu.contains(e.target as Node)) hidePictureMenu(); });
document.addEventListener('keydown', e => { if (e.key === 'Escape') hidePictureMenu(); });
stage.on('wheel dragstart', hidePictureMenu);

// ---- Moves from tokens ---------------------------------------------------------------------------------------------

// While a fight is on, right-clicking your own character's token offers every move they can make: the basic moves and
// their rolled class moves. An enemy's offers its attack if you run the fight; anyone else's offers nothing. For a move
// aimed at someone, click who, then add any bonus, and the bot rolls it by the same rules as /move and tells game chat.
// Tokens placed when the fight started know who they are (their `fighter` attribute, like "enemy:12"); ones made by
// hand go by their name. The server says which moves a token has, and has the last word on making them.

const AIM_COLOR = '#D85A30';

const fightOn = () => document.querySelector<HTMLElement>('[data-encounter-panel]')?.dataset.fight === 'true';
const fighterOf = (token: Konva.Node) => ((token.getAttr('fighter') as string | undefined)?.split(':') ?? []) as [string?, string?];
// With sign-in off everyone's a guest, and anyone can do anything in a fight
const isGuest = () => !live || live.me.id.startsWith('guest-');

/** Whether it's worth asking the server for a token's moves: not for someone else's character, or an enemy you don't run. */
function mightHaveMoves(token: Konva.Node) {
    if (!live || !fightOn()) return false;
    const [kind, id] = fighterOf(token);
    if (kind === 'character') return isGuest() || live.me.id === id;
    if (kind === 'enemy') return isGuest() || live.me.isDungeonMaster;
    return kind === undefined;
}

// How big a token is on the board: its circle's radius, scaled with it
function radiusOf(token: Konva.Node) {
    const circle = token instanceof Konva.Circle ? token : (token as Konva.Group).findOne<Konva.Circle>('Circle');
    return (circle?.radius() ?? TOKEN_RADIUS) * token.scaleX();
}

// Kept inside the board, so one opened near the edge isn't cut off
function placeMenu(menu: HTMLElement, at: { x: number; y: number }) {
    const box = container.getBoundingClientRect();
    menu.style.left = `${Math.max(4, Math.min(at.x - box.left, box.width - menu.offsetWidth - 4))}px`;
    menu.style.top = `${Math.max(4, Math.min(at.y - box.top, box.height - menu.offsetHeight - 4))}px`;
}

const moveMenu = document.createElement('div');
moveMenu.className = 'board-menu';
moveMenu.hidden = true;
moveMenu.setAttribute('role', 'menu');
container.append(moveMenu);

// A right-click whose moves come back after the menu was closed, or another right-click, is dropped
let menuAsk = 0;

/** Opens the token's moves, if it has any for you: its basic moves, then its class moves. */
async function showMoveMenu(token: Konva.Node, at: { x: number; y: number }) {
    const ask = ++menuAsk;
    const moves = await live!.movesFor(token.id());
    if (ask !== menuAsk || !moves.length) return false;
    const button = (move: BoardMove) => {
        const item = Object.assign(document.createElement('button'), { type: 'button', textContent: `${move.name}…` });
        item.setAttribute('role', 'menuitem');
        if (move.modifier != null) {
            const odds = chances(move.modifier);
            item.append(Object.assign(document.createElement('span'), {
                className: `odds ${oddsClass(odds.hit)}`,
                textContent: `${signed(move.modifier)} · ${percent(odds.hit)}`,
                title: `${percent(odds.hit)} to hit (7+), ${percent(odds.strong)} strong (10+)`,
            }));
        }
        item.addEventListener('click', () => {
            hideMoveMenu();
            if (move.aim === 'none') showMoveForm(token, null, move, at);
            else startAiming(token, move);
        });
        return item;
    };
    const basic = moves.filter(m => m.kind !== 'classMove').map(button);
    const classMoves = moves.filter(m => m.kind === 'classMove').map(button);
    const divider = Object.assign(document.createElement('hr'), { className: 'divider' });
    moveMenu.replaceChildren(...basic, ...(basic.length && classMoves.length ? [divider] : []), ...classMoves);
    moveMenu.hidden = false;
    placeMenu(moveMenu, at);
    // Focusing mustn't scroll the page under the board, which would move everything away from the pointer
    (moveMenu.firstElementChild as HTMLElement).focus({ preventScroll: true });
    return true;
}

function hideMoveMenu() { moveMenu.hidden = true; menuAsk++; }

// Picking who it's aimed at: an arrow follows the pointer until a token is clicked. Anywhere else, or Escape, cancels.
let aiming: { actor: Konva.Node; move: BoardMove; arrow: Konva.Arrow; ring: Konva.Circle } | null = null;
const aimHint = Object.assign(document.createElement('div'), { className: 'board-hint', hidden: true });
container.append(aimHint);

function startAiming(actor: Konva.Node, move: BoardMove) {
    stopAiming();
    setTool('move');
    select(null);
    const arrow = new Konva.Arrow({
        points: [actor.x(), actor.y(), actor.x(), actor.y()],
        stroke: AIM_COLOR, fill: AIM_COLOR, strokeWidth: 3, dash: [12, 8], pointerLength: 14, pointerWidth: 14, opacity: 0.85,
    });
    // Around whichever token the pointer is over, so you can see who you'd pick
    const ring = new Konva.Circle({ stroke: AIM_COLOR, strokeWidth: 3, opacity: 0.85, visible: false });
    others.add(arrow, ring);
    aiming = { actor, move, arrow, ring };
    // Clicking a token picks it rather than picking it up
    for (const token of tokens.getChildren()) token.draggable(false);
    aimHint.textContent = `${move.name}: click who ${actor.name() || 'it'} is ${move.aim === 'ally' ? 'helping or hindering' : 'aiming at'} · Esc cancels`;
    aimHint.hidden = false;
    container.classList.add('aiming');
}

function stopAiming() {
    if (!aiming) return;
    aiming.arrow.destroy();
    aiming.ring.destroy();
    aiming = null;
    for (const token of tokens.getChildren()) token.draggable(canDrag(token));
    aimHint.hidden = true;
    container.classList.remove('aiming');
}

stage.on('pointermove', () => {
    if (!aiming) return;
    const { x, y } = pointer();
    const { actor, arrow, ring } = aiming;
    const over = stage.getIntersection(stage.getPointerPosition() ?? { x: 0, y: 0 });
    const hovered = over?.getLayer() === tokens ? itemOf(over) : null;
    if (!hovered || hovered === actor) {
        ring.visible(false);
        arrow.points([actor.x(), actor.y(), x, y]);
        return;
    }
    // Over a token: ring it, and the arrow stops at the ring rather than running on to the pointer
    const radius = radiusOf(hovered) + 8;
    ring.position(hovered.position()).radius(radius).visible(true);
    const dx = actor.x() - hovered.x(), dy = actor.y() - hovered.y();
    const distance = Math.hypot(dx, dy) || 1;
    const reach = Math.max(0, distance - radius) / distance;
    arrow.points([actor.x(), actor.y(), actor.x() - dx * reach, actor.y() - dy * reach]);
});

stage.on('pointerdown', e => {
    if (!aiming || e.evt.button !== 0) return;
    const target = e.target.getLayer() === tokens ? itemOf(e.target) : null;
    if (target === aiming.actor) return;
    const { actor, move } = aiming;
    stopAiming();
    if (!target) return;
    // The same press reaches the page next, which mustn't take it for a press outside the form
    formOpenedBy = e.evt;
    showMoveForm(actor, target, move, { x: e.evt.clientX, y: e.evt.clientY });
});

// The last step: any bonus for a player's move, or what an enemy does, then roll
const moveForm = Object.assign(document.createElement('form'), { className: 'board-menu move-form', hidden: true });
container.append(moveForm);
let formOpenedBy: Event | null = null;

function field(label: string, input: HTMLInputElement | HTMLSelectElement) {
    const wrapper = document.createElement('label');
    wrapper.append(Object.assign(document.createElement('span'), { textContent: label }), input);
    return wrapper;
}

const numberInput = (name: string) =>
    Object.assign(document.createElement('input'), { type: 'number', name, value: '0', min: '-10', max: '10', step: '1' });

// For Defy Danger: how they do it decides the stat, as /move defy-danger puts it
const STATS: [Stat, string][] = [
    ['str', 'STR: power through'], ['dex', 'DEX: dodge'], ['con', 'CON: endure'],
    ['int', 'INT: think fast'], ['wis', 'WIS: resist'], ['cha', 'CHA: charm'],
];

function statSelect() {
    const select = Object.assign(document.createElement('select'), { name: 'stat', className: 'filter' });
    select.append(...STATS.map(([value, label]) => Object.assign(document.createElement('option'), { value, textContent: label })));
    return select;
}

// ---- Odds -----------------------------------------------------------------------------------------------------------

// How likely 2d6 plus a modifier is to hit (7+) and to hit strongly (10+): out of the 36 ways two dice land
function chances(modifier: number) {
    const atLeast = (target: number) => {
        let ways = 0;
        for (let sum = 2; sum <= 12; sum++) if (sum + modifier >= target) ways += 6 - Math.abs(sum - 7);
        return ways / 36;
    };
    return { hit: atLeast(7), strong: atLeast(10) };
}
const percent = (share: number) => `${Math.round(share * 100)}%`;
const signed = (n: number) => (n < 0 ? `−${-n}` : `+${n}`);
const oddsClass = (hit: number) => (hit >= 0.75 ? 'good' : hit >= 0.5 ? 'fair' : 'poor');

// The chance to hit under the form's fields, kept up to date as the bonus or stat changes
function oddsLine(move: BoardMove) {
    const line = Object.assign(document.createElement('div'), { className: 'odds-line' });
    const update = () => {
        const data = new FormData(moveForm);
        const base = move.picksStat ? move.statModifiers?.[(data.get('stat') as string) ?? 'str'] : move.modifier;
        if (base == null) { line.hidden = true; return; }
        const modifier = base + (Number(data.get('bonus') ?? 0) || 0);
        const odds = chances(modifier);
        line.hidden = false;
        line.className = `odds-line ${oddsClass(odds.hit)}`;
        line.replaceChildren(
            Object.assign(document.createElement('strong'), { textContent: `${percent(odds.hit)} to hit` }),
            Object.assign(document.createElement('span'), { textContent: ` · ${percent(odds.strong)} strong · 2d6${signed(modifier)}` }));
    };
    return { line, update };
}

function showMoveForm(actor: Konva.Node, target: Konva.Node | null, move: BoardMove, at: { x: number; y: number }) {
    const title = Object.assign(document.createElement('strong'), { textContent: target ? `${move.name} → ${target.name() || 'token'}` : move.name });
    const fields = move.kind === 'enemyAttack'
        ? [field('What it does', Object.assign(document.createElement('input'), { type: 'text', name: 'move', placeholder: 'Attack', maxLength: 80 }))]
        : [
            ...(move.picksStat ? [field('How', statSelect())] : []),
            field('Bonus', numberInput('bonus')),
            ...(move.rollsDamage ? [field('Extra damage', numberInput('extraDamage'))] : []),
        ];
    const roll = Object.assign(document.createElement('button'), { type: 'submit', className: 'roll', textContent: '🎲 Roll' });
    const cancel = Object.assign(document.createElement('button'), { type: 'button', textContent: 'Cancel' });
    cancel.addEventListener('click', hideMoveForm);
    const actions = Object.assign(document.createElement('div'), { className: 'actions' });
    actions.append(cancel, roll);
    const odds = move.kind === 'enemyAttack' ? null : oddsLine(move);
    moveForm.replaceChildren(title, ...fields, ...(odds ? [odds.line] : []), actions);
    moveForm.oninput = () => odds?.update();
    odds?.update();
    moveForm.onsubmit = e => {
        e.preventDefault();
        const data = new FormData(moveForm);
        hideMoveForm();
        makeMove({
            kind: move.kind,
            actor: actor.id(),
            target: target?.id(),
            bonus: Number(data.get('bonus') ?? 0) || 0,
            extraDamage: Number(data.get('extraDamage') ?? 0) || 0,
            move: move.kind === 'enemyAttack' ? (data.get('move') as string | null)?.trim() || undefined : move.name,
            stat: (data.get('stat') as Stat | null) ?? undefined,
        });
    };
    moveForm.hidden = false;
    placeMenu(moveForm, at);
    const first = moveForm.querySelector<HTMLInputElement | HTMLSelectElement>('input, select');
    first?.focus({ preventScroll: true });
    if (first instanceof HTMLInputElement) first.select();
}

function hideMoveForm() { moveForm.hidden = true; }

async function makeMove(ask: MoveAsk) {
    if (!live) return showToast("You're offline, so the bot can't roll it.");
    // A move refused while an earlier one is still rolling leaves that one's ring alone
    const alreadyRolling = rolling.has(ask.actor);
    showRolling(ask.actor);
    try {
        await live.makeMove(ask);
    } catch (e) {
        if (!alreadyRolling) stopRolling(ask.actor);
        showToast((e as Error).message);
    }
}

// A dashed ring turns around a token while the bot rolls its move, for everyone looking
const rolling = new Map<string, { ring: Konva.Circle; spin: Konva.Animation }>();

function showRolling(tokenId: string) {
    const token = stage.findOne('#' + tokenId);
    if (!token || rolling.has(tokenId)) return;
    const ring = new Konva.Circle({
        x: token.x(), y: token.y(), radius: radiusOf(token) + 8,
        stroke: AIM_COLOR, strokeWidth: 3, dash: [10, 8], listening: false,
    });
    others.add(ring);
    const spin = new Konva.Animation(frame => { ring.rotation((frame?.time ?? 0) / 8); }, others);
    spin.start();
    rolling.set(tokenId, { ring, spin });
    // If the answer never comes (a dropped connection), it doesn't spin forever
    setTimeout(() => { if (rolling.get(tokenId)?.ring === ring) stopRolling(tokenId); }, 90_000);
}

function stopRolling(tokenId: string) {
    const shown = rolling.get(tokenId);
    if (!shown) return;
    shown.spin.stop();
    shown.ring.destroy();
    rolling.delete(tokenId);
}

// What it did floats up off the target and fades: red for damage, gray for a miss
function floatOver(tokenId: string, text: string) {
    const token = stage.findOne('#' + tokenId);
    if (!token) return;
    const radius = radiusOf(token);
    const label = new Konva.Text({
        x: token.x() - 150, y: token.y() - radius - 34, width: 300, align: 'center',
        text, fontSize: 24, fontStyle: 'bold', fontFamily: 'system-ui, sans-serif',
        fill: text.includes('-') ? '#d1453b' : text === 'Miss' ? '#6b665d' : '#2f9e5b',
        stroke: '#ffffff', strokeWidth: 4, fillAfterStrokeEnabled: true, listening: false,
    });
    others.add(label);
    new Konva.Tween({ node: label, duration: 2.4, y: label.y() - 40, opacity: 0, easing: Konva.Easings.EaseIn, onFinish: () => label.destroy() }).play();
}

function showMoveResult(result: MoveResult) {
    stopRolling(result.actor);
    if (result.summary) floatOver(result.target, result.summary);
    // Only whoever made it needs to know why it didn't happen
    if (result.error && result.personID === live?.me.id) showToast(result.error);
}

const toast = Object.assign(document.createElement('div'), { className: 'board-toast', hidden: true });
toast.setAttribute('role', 'status');
container.append(toast);
let toastTimer = 0;

function showToast(text: string) {
    toast.textContent = text;
    toast.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = window.setTimeout(() => { toast.hidden = true; }, 5000);
}

// Like any menu: pressing anywhere else, Escape, or moving the board closes them
document.addEventListener('pointerdown', e => {
    if (!moveMenu.contains(e.target as Node)) hideMoveMenu();
    if (!moveForm.contains(e.target as Node) && e !== formOpenedBy) hideMoveForm();
});
document.addEventListener('keydown', e => {
    if (e.key !== 'Escape') return;
    hideMoveMenu();
    hideMoveForm();
    stopAiming();
});
stage.on('wheel dragstart', () => { hideMoveMenu(); hideMoveForm(); });

// ---- Text ----------------------------------------------------------------------------------------------------------

const TEXT_SIZE = 22;

// Typing happens in a real <textarea> laid over the board at the right spot and zoom, since a canvas can't take
// typing itself. Enter (or clicking away) puts it on the board as a Konva.Text; Escape cancels; Shift+Enter adds a line.
function editText(existing: Konva.Text | null, at: { x: number; y: number }) {
    // Where that board point is on screen, after zoom and pan
    const screen = stage.getAbsoluteTransform().point(at);
    const scale = stage.scaleX();

    const editor = document.createElement('textarea');
    editor.className = 'board-text-editor';
    editor.rows = 1;
    editor.value = existing?.text() ?? '';
    Object.assign(editor.style, {
        left: `${screen.x}px`,
        top: `${screen.y}px`,
        fontSize: `${(existing?.fontSize() ?? TEXT_SIZE) * scale}px`,
        color: (existing?.fill() as string | undefined) ?? color,
    });
    existing?.hide();                                      // so the old words don't show through while editing
    container.append(editor);
    // After this click has finished, or the board takes the focus straight back
    setTimeout(() => editor.focus());

    let finished = false;
    function finish(save: boolean) {
        if (finished) return;
        finished = true;
        const words = editor.value.trim();
        editor.remove();

        if (existing) {
            existing.show();
            if (!save || words === existing.text()) return;
            if (!words) {                                  // emptied out: it goes
                removeMine(existing);
                return;
            }
            remember('changed', existing);
            existing.text(words);
            live?.addItem(existing.id(), 'text', existing.toObject());   // the same ID replaces it for everyone
            return;
        }

        if (!save || !words) return;
        const text = new Konva.Text({
            id: crypto.randomUUID(), x: at.x, y: at.y, text: words,
            fontSize: TEXT_SIZE, fontFamily: 'system-ui, sans-serif', fill: color,
        });
        drawing.add(text);
        live?.addItem(text.id(), 'text', text.toObject());
        remember('added', text);
    }

    editor.addEventListener('keydown', e => {
        if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); finish(true); }
        if (e.key === 'Escape') finish(false);
    });
    editor.addEventListener('blur', () => finish(true));
}

// ---- Tokens --------------------------------------------------------------------------------------------------------

// "Goblin boss" is GB, "Kael" is K: the first letter of up to two words
const initialsOf = (name: string) =>
    name.split(/\s+/).filter(Boolean).slice(0, 2).map(word => [...word][0]).join('').toUpperCase();

// Their initials in white, centered in a token's circle and sized to it
function addInitials(token: Konva.Group, name: string, radius: number) {
    token.add(new Konva.Text({
        text: initialsOf(name), fontSize: Math.round(radius * 0.8), fontStyle: 'bold', fill: '#ffffff',
        width: radius * 2, height: radius * 2, offsetX: radius, offsetY: radius,
        align: 'center', verticalAlign: 'middle', listening: false,
    }));
}

// Tokens without a picture show initials, including ones saved as a bare circle (like the samples) or before tokens
// had them. The circle is wrapped in a group just for showing; it sits at the same spot, so moves still line up.
function withInitials(token: Konva.Node): Konva.Node {
    if (srcOf(token)) return token;
    if (token instanceof Konva.Circle) {
        const group = new Konva.Group({ x: token.x(), y: token.y(), name: token.name() });
        token.position({ x: 0, y: 0 });
        token.name('');
        // Old saves have the circle draggable, and Konva drags the innermost draggable thing, which would leave the
        // group (the saved item) where it was
        token.draggable(false);
        group.add(token);
        addInitials(group, group.name(), token.radius());
        return group;
    }
    if (token instanceof Konva.Group) {
        const circle = token.findOne<Konva.Circle>('Circle');
        if (circle && !srcOf(circle) && !token.findOne('Text')) addInitials(token, token.name(), circle.radius());
    }
    return token;
}

// New tokens start big enough to read their initials at a glance (a grid square and a half across); the corner
// handles shrink them for a crowded board
const TOKEN_RADIUS = 36;

// A colored circle with their initials, or with a picture in it if there's one
// Characters' pictures by their names in lowercase, from the page: a token named for a character shows their picture
const portraits: Record<string, string> = JSON.parse(container.dataset.portraits || '{}');

function placeToken(x: number, y: number, picture?: string, name = window.prompt('Token name')?.trim()) {
    if (!name) return;
    picture ??= portraits[name.toLowerCase()];

    const token = new Konva.Group({ id: crypto.randomUUID(), x, y, name });
    const circle = new Konva.Circle({ radius: TOKEN_RADIUS, fill: color, stroke: '#ffffff', strokeWidth: 2 });
    if (picture) circle.setAttr('src', picture);
    token.add(circle);
    if (!picture) addInitials(token, name, circle.radius());
    prepare(token, 'token');
    tokens.add(token);
    listTokens();
    live?.addItem(token.id(), 'token', token.toObject());
    remember('added', token);
}

// Others see a dragged token or text move about 12 times a second; where it's dropped is always sent
let lastMove = 0;
// Where it was picked up from, for undo
let beforeDrag: ItemJson | null = null;
for (const layer of [tokens, drawing, pictures]) {
    layer.on('dragstart', e => { beforeDrag = snapshot(e.target); });
    layer.on('dragmove', e => {
        // Dragging takes over the pointer, so the stage's pointermove doesn't send the cursor meanwhile
        const { x, y } = pointer();
        live?.cursor(x, y);
        if (Date.now() - lastMove < 80) return;
        lastMove = Date.now();
        live?.moveItem(e.target.id(), e.target.x(), e.target.y());
    });
    layer.on('dragend', e => {
        // A press that didn't move it isn't worth an undo
        const from = beforeDrag?.attrs as { x?: number; y?: number } | undefined;
        if (beforeDrag && (e.target.x() !== (from?.x ?? 0) || e.target.y() !== (from?.y ?? 0))) remember('changed', e.target, beforeDrag);
        beforeDrag = null;
        live?.moveItem(e.target.id(), e.target.x(), e.target.y());
    });
}

// ---- Adding pictures -----------------------------------------------------------------------------------------------

// Pictures are shrunk in the browser before uploading: plenty sharp on the board, and small to store and send.
// The server only checks the file really is a picture; it never has to decode one.
const TOKEN_PICTURE_SIZE = 512;
const BOARD_PICTURE_SIZE = 2560;

async function shrink(file: File, largest: number) {
    const bitmap = await createImageBitmap(file);            // fails for anything that isn't a picture
    const scale = Math.min(1, largest / Math.max(bitmap.width, bitmap.height));
    const canvas = new OffscreenCanvas(Math.round(bitmap.width * scale), Math.round(bitmap.height * scale));
    canvas.getContext('2d')!.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    bitmap.close();
    // WebP where the browser can make it (Safari can't, and gives PNG instead)
    return { blob: await canvas.convertToBlob({ type: 'image/webp', quality: 0.85 }), width: canvas.width, height: canvas.height };
}

// The form's antiforgery token, from <AntiforgeryToken /> on the page, which the upload has to send back
const antiforgery = () => document.querySelector<HTMLInputElement>('input[name="__RequestVerificationToken"]')?.value ?? '';

async function upload(file: File, largest: number) {
    const { blob, width, height } = await shrink(file, largest);
    const form = new FormData();
    form.append('file', blob, 'picture');
    form.append('__RequestVerificationToken', antiforgery());
    const response = await fetch(`api/canvases/${canvasId}/images`, { method: 'POST', body: form });
    if (!response.ok) throw new Error((await response.text()) || `Upload failed (${response.status})`);
    const { url } = await response.json() as { url: string };
    return { url, width, height };
}

// The board point in the middle of what you can see
const viewCenter = () => stage.getAbsoluteTransform().copy().invert().point({ x: stage.width() / 2, y: stage.height() / 2 });

// A map or handout on the board, centered where you're looking (or where it was dropped), at most 800 by 600 units
async function addPicture(file: File, at = viewCenter()) {
    const { url, width, height } = await upload(file, BOARD_PICTURE_SIZE);
    const scale = Math.min(1, 800 / width, 600 / height);
    const picture = new Konva.Image({
        id: crypto.randomUUID(), image: undefined,
        x: Math.round(at.x - (width * scale) / 2), y: Math.round(at.y - (height * scale) / 2),
        width: Math.round(width * scale), height: Math.round(height * scale),
    });
    picture.setAttr('src', url);
    prepare(picture, 'image');
    pictures.add(picture);
    live?.addItem(picture.id(), 'image', picture.toObject());
    remember('added', picture);
}

async function addPictureToken(file: File, at = viewCenter()) {
    const name = window.prompt('Token name')?.trim();
    if (!name) return;
    const { url } = await upload(file, TOKEN_PICTURE_SIZE);
    placeToken(at.x, at.y, url, name);
}

// A picture for the enemies being added from the fight's dialog goes up the same way as an image token's. The form
// then carries its address, and the server puts it on each enemy's token.
document.querySelector<HTMLFormElement>('#add-enemies form')?.addEventListener('submit', async e => {
    const form = e.currentTarget as HTMLFormElement;
    const file = form.querySelector<HTMLInputElement>('[data-enemy-picture]')?.files?.[0];
    if (!file) return;

    e.preventDefault();
    const add = form.querySelector<HTMLButtonElement>('.create');
    if (add) { add.disabled = true; add.textContent = 'Uploading…'; }
    try {
        const { url } = await upload(file, TOKEN_PICTURE_SIZE);
        form.querySelector<HTMLInputElement>('input[name="pictureUrl"]')!.value = url;
        form.submit();                                     // doesn't fire submit again
    } catch (err) {
        window.alert(`Couldn't add that picture: ${err instanceof Error ? err.message : err}`);
        if (add) { add.disabled = false; add.textContent = 'Add'; }
    }
});

// A picture on the board, or a token's picture (only from the sidebar's "+ Image token")
function addFile(file: File | undefined, asToken: boolean, at?: { x: number; y: number }) {
    if (viewOnly || !file?.type.startsWith('image/')) return;
    (asToken ? addPictureToken(file, at) : addPicture(file, at))
        .catch(err => window.alert(`Couldn't add that picture: ${err instanceof Error ? err.message : err}`));
}

// The rail's picture button and the sidebar's "+ Image token" share one hidden file picker
const filePicker = document.querySelector<HTMLInputElement>('[data-file]')!;
let pickingToken = false;
document.querySelector('.board-page')!.addEventListener('click', e => {
    const button = (e.target as HTMLElement).closest<HTMLElement>('[data-pick]');
    if (!button || viewOnly) return;
    pickingToken = button.dataset.pick === 'token';
    filePicker.value = '';
    filePicker.click();
});
filePicker.addEventListener('change', () => addFile(filePicker.files?.[0], pickingToken));

// Dropping a picture on the board puts it where it was dropped
container.addEventListener('dragover', e => e.preventDefault());
container.addEventListener('drop', e => {
    e.preventDefault();
    stage.setPointersPositions(e);
    addFile(e.dataTransfer?.files[0], false, stage.getRelativePointerPosition() ?? undefined);
});

// Pasting a picture (like a screenshot) puts it in the middle of what you can see
document.addEventListener('paste', e => {
    if ((e.target as HTMLElement).closest('input, textarea, select, [contenteditable], dialog')) return;
    const file = [...e.clipboardData?.files ?? []].find(f => f.type.startsWith('image/'));
    if (file) { e.preventDefault(); addFile(file, false); }
});

// ---- Watching a fight you're not in -------------------------------------------------------------------------------

// While a fight is on an encounter scene, only the DM and those in it can change its board (FightBoard on the server
// refuses anyone else). Everyone else can still look, pan and zoom. The encounter panel says which, and app.js swaps in
// a fresh panel as fights start and end, so this looks again whenever it does.
function checkViewOnly() {
    const next = document.querySelector<HTMLElement>('[data-encounter-panel]')?.dataset.viewOnly === 'true';
    if (next === viewOnly) return;
    viewOnly = next;
    document.querySelector('.board-page')!.classList.toggle('view-only', viewOnly);
    select(null);
    setTool('move');
    listTokens();
}
checkViewOnly();
const boardSide = document.querySelector('.board-side');
// Whoever's defeated can change with any fresh panel too, whether or not you can change the board
if (boardSide) new MutationObserver(() => { checkViewOnly(); showDefeated(); showWounds(); showBadges(); }).observe(boardSide, { childList: true });

// ---- Condition badges ------------------------------------------------------------------------------------------------

// A little pill of icons at the top right of a fighter's token: a character's debilities (🫨 Shaky) or 0 HP, and how hurt
// an enemy looks (🤕 🩸 💀). The encounter panel says which; they follow their token as it's dragged around.

function showBadges() {
    const wanted = new Map<Konva.Node, { icons: string; title: string }>();
    for (const li of document.querySelectorAll<HTMLElement>('[data-encounter-panel] [data-wounds] li')) {
        const icons = li.dataset.badges?.trim();
        const token = icons && tokenFor(li.dataset.fighter!, li.dataset.name ?? '');
        if (token) wanted.set(token, { icons, title: li.dataset.badgeTitle ?? '' });
    }
    for (const [token, label] of badgeFor) {
        if (wanted.has(token) && token.getLayer()) continue;
        label.destroy();
        badgeFor.delete(token);
    }
    for (const [token, { icons }] of wanted) {
        let label = badgeFor.get(token);
        if (!label) {
            label = new Konva.Label();
            label.add(new Konva.Tag({ fill: themeColor('--surface', '#1c1714'), stroke: themeColor('--border', '#3a2e27'), strokeWidth: 1, cornerRadius: 999, shadowColor: 'black', shadowBlur: 4, shadowOpacity: .35 }));
            label.add(new Konva.Text({ fontSize: 15, padding: 3, fontFamily: 'system-ui, sans-serif' }));
            badges.add(label);
            badgeFor.set(token, label);
        }
        label.getText().text(icons);
    }
    placeBadges();
}

// Over each token's top right edge, the same size on screen however far you're zoomed
function placeBadges() {
    const scale = 1 / stage.scaleX();
    for (const [token, label] of badgeFor) {
        const box = token.getClientRect({ relativeTo: tokens });
        label.scale({ x: scale, y: scale });
        label.position({ x: box.x + box.width * .78 - label.width() * scale / 2, y: box.y - label.height() * scale * .35 });
    }
    badges.batchDraw();
}
// Tokens move (dragged, or by someone else) and the board zooms, so the badges follow whenever the tokens are drawn
tokens.on('draw', placeBadges);

// ---- Damage floaters -------------------------------------------------------------------------------------------------

// When someone in the fight is hurt or healed, the number floats up off their token and fades, and the token flashes.
// The encounter panel lists how hurt each fighter is (how far below their max, never their max itself, which players
// don't see for enemies), and each fresh panel is compared with the last.
let lastWounds: Map<string, number> | null = null;

function readWounds() {
    const wounds = new Map<string, number>();
    const names = new Map<string, string>();
    for (const li of document.querySelectorAll<HTMLElement>('[data-encounter-panel] [data-wounds] li')) {
        wounds.set(li.dataset.fighter!, Number(li.dataset.hurt));
        names.set(li.dataset.fighter!, li.dataset.name ?? '');
    }
    return { wounds, names };
}

// Tokens placed by the fight know who they are; ones made by hand go by their name
function tokenFor(fighter: string, name: string) {
    const all: Konva.Node[] = tokens.getChildren();
    return all.find(t => t.getAttr('fighter') === fighter)
        ?? all.find(t => !t.getAttr('fighter') && !!name && t.name().toLowerCase() === name.toLowerCase());
}

function showWounds() {
    const { wounds, names } = readWounds();
    const before = lastWounds;
    lastWounds = wounds;
    // The first look (and a fight just starting) only learns where everyone's at
    if (!before) return;
    for (const [fighter, hurt] of wounds) {
        const was = before.get(fighter);
        if (was === undefined || was === hurt) continue;
        const token = tokenFor(fighter, names.get(fighter) ?? '');
        if (token) floatOff(token, was - hurt);
    }
}

// A declaration rather than a const, since badges (drawn whenever the token list is) can need it before this line runs
function themeColor(name: string, fallback: string) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback;
}

// Negative is damage, positive healing
function floatOff(token: Konva.Node, change: number) {
    const box = token.getClientRect({ relativeTo: tokens });
    const center = { x: box.x + box.width / 2, y: box.y + box.height / 2 };
    const color = change < 0 ? themeColor('--critical', '#d1453b') : themeColor('--good', '#2f9e5b');
    // The same size on screen however far in or out you're zoomed
    const scale = 1 / stage.scaleX();
    const still = matchMedia('(prefers-reduced-motion: reduce)').matches;

    const ring = new Konva.Circle({ ...center, radius: box.width / 2, stroke: color, strokeWidth: 6 * scale, opacity: .9 });
    const text = new Konva.Text({
        text: change < 0 ? `−${-change}` : `+${change}`,
        x: center.x, y: box.y - 6 * scale,
        fontSize: 30 * scale, fontStyle: 'bold', fontFamily: 'system-ui, sans-serif',
        fill: color, stroke: 'rgba(0, 0, 0, .55)', strokeWidth: 4 * scale, fillAfterStrokeEnabled: true,
    });
    text.offset({ x: text.width() / 2, y: text.height() });
    effects.add(ring, text);

    if (still) {
        setTimeout(() => { ring.destroy(); text.destroy(); }, 1500);
        return;
    }
    // A hit punches the token's ring out; the number pops up, rises and fades
    new Konva.Tween({ node: ring, duration: .6, scaleX: 1.45, scaleY: 1.45, opacity: 0, easing: Konva.Easings.EaseOut, onFinish: () => ring.destroy() }).play();
    text.scale({ x: .4, y: .4 });
    new Konva.Tween({ node: text, duration: .25, scaleX: 1.15, scaleY: 1.15, easing: Konva.Easings.BackEaseOut,
        onFinish: () => new Konva.Tween({ node: text, duration: 1.3, y: text.y() - 46 * scale, scaleX: 1, scaleY: 1, opacity: 0,
            easing: Konva.Easings.EaseIn, onFinish: () => text.destroy() }).play() }).play();
    // And the token itself shakes on a hit
    if (change < 0) {
        const x = token.x();
        const shake = new Konva.Animation(frame => {
            const t = frame!.time;
            // Picked up mid-shake: leave it where the drag takes it
            if (token.isDragging()) { shake.stop(); return; }
            if (t > 400) { token.x(x); shake.stop(); return; }
            token.x(x + Math.sin(t / 25) * 5 * scale * (1 - t / 400));
        }, tokens);
        shake.start();
    }
}
showWounds();
showBadges();

// ---- The party's trail (map scenes) -----------------------------------------------------------------------------------

// A map has one pin for the party. Dragging it somewhere new adds where it was to a dotted trail behind it, so the map
// shows the road they've traveled, older stops fading. Both are ordinary items (the pin a token marked partyPin, the
// trail a drawing marked partyTrail), so they're saved and shared like anything else on the board.
const TRAIL_COLOR = '#e8a33a';
// Shorter hops than this are tidying the pin's spot, not traveling
const TRAIL_STEP = 24;

const partyPin = () => (tokens.getChildren() as Konva.Node[]).find(t => t.getAttr('partyPin')) as Konva.Group | undefined;
const partyTrail = () => (drawing.getChildren() as Konva.Node[]).find(t => t.getAttr('partyTrail')) as Konva.Group | undefined;

document.querySelector('[data-party-pin]')?.addEventListener('click', () => {
    let pin = partyPin();
    if (!pin) {
        // In the middle of what you can see
        const at = { x: (stage.width() / 2 - stage.x()) / stage.scaleX(), y: (stage.height() / 2 - stage.y()) / stage.scaleY() };
        pin = new Konva.Group({ id: crypto.randomUUID(), ...at, name: 'The party', partyPin: true });
        pin.add(new Konva.Circle({ radius: TOKEN_RADIUS * .8, fill: TRAIL_COLOR, stroke: '#ffffff', strokeWidth: 3, shadowColor: 'black', shadowBlur: 8, shadowOpacity: .4 }));
        pin.add(new Konva.Text({ text: '🧭', fontSize: TOKEN_RADIUS * .9, x: -TOKEN_RADIUS * .45, y: -TOKEN_RADIUS * .45, listening: false }));
        prepare(pin, 'token');
        tokens.add(pin);
        listTokens();
        live?.addItem(pin.id(), 'token', pin.toObject());
        remember('added', pin);
    }
    select(pin);
});

let pinFrom: { x: number; y: number } | null = null;
tokens.on('dragstart', e => { const node: Konva.Node = e.target; pinFrom = node.getAttr('partyPin') ? node.position() : null; });
tokens.on('dragend', e => {
    const node: Konva.Node = e.target;
    if (!pinFrom || !node.getAttr('partyPin')) return;
    const from = pinFrom, to = node.position();
    pinFrom = null;
    if (Math.hypot(to.x - from.x, to.y - from.y) < TRAIL_STEP) return;

    // The stops so far, then where it's just left, ending at where it is now
    const old = partyTrail();
    const stops: { x: number; y: number; at: string }[] = old?.getAttr('stops') ?? [];
    stops.push({ ...from, at: new Date().toISOString() });
    const trail = drawTrail(old?.id() ?? crypto.randomUUID(), stops, to);
    old?.destroy();
    drawing.add(trail);
    live?.addItem(trail.id(), 'stroke', trail.toObject());
});

// A dashed path through every stop to the pin, with a dot at each stop, fainter the longer ago it was
function drawTrail(id: string, stops: { x: number; y: number; at: string }[], to: { x: number; y: number }) {
    const trail = new Konva.Group({ id, name: 'Party trail', partyTrail: true, stops, listening: false });
    trail.add(new Konva.Line({
        points: [...stops.flatMap(s => [s.x, s.y]), to.x, to.y],
        stroke: TRAIL_COLOR, strokeWidth: 4, dash: [2, 12], lineCap: 'round', lineJoin: 'round', opacity: .9,
        tension: .25,
    }));
    stops.forEach((s, i) => trail.add(new Konva.Circle({
        x: s.x, y: s.y, radius: 7, fill: TRAIL_COLOR, stroke: '#ffffff', strokeWidth: 2,
        opacity: .35 + .65 * ((i + 1) / stops.length),
    })));
    return trail;
}

// ---- Thumbnail for the canvases page --------------------------------------------------------------------------

// A couple of seconds after your last change, the board takes a small picture of itself for its card on the canvases
// page: framed on what's drawn (see thumbnailFrame), wherever you're zoomed or panned to, without cursors or handles.
const THUMBNAIL_WIDTH = 480;
let thumbnailTimer = 0;

function thumbnailSoon() {
    clearTimeout(thumbnailTimer);
    thumbnailTimer = window.setTimeout(() => saveThumbnail().catch(() => { /* only the card misses out */ }), 2000);
}

// The part of the board worth showing, in board units: everything on it, with room around it, in the card's shape
// (16:10), never smaller than a third of where boards start so one scribble isn't blown up. An empty board shows
// where boards start.
function thumbnailFrame() {
    const content = contentBounds();
    return content ? frameAround(content, 40, HOME.width / 3, HOME.width / HOME.height) : HOME;
}

async function saveThumbnail() {
    const scale = stage.scaleX();
    const frame = thumbnailFrame();
    handles.hide();
    others.hide();
    effects.hide();
    badges.hide();
    try {
        // The grid is only drawn where you're looking; draw it under the frame instead, just while the picture is
        // drawn (which happens before toBlob returns), so the board on screen never shows it
        gridArea = frame;
        // Where that frame is on screen right now; Konva draws all of it, even parts panned out of view
        const picture = stage.toBlob({
            x: stage.x() + frame.x * scale, y: stage.y() + frame.y * scale,
            width: frame.width * scale, height: frame.height * scale,
            pixelRatio: THUMBNAIL_WIDTH / (frame.width * scale), mimeType: 'image/webp', quality: 0.8,
        });
        gridArea = null;
        const blob = await picture as Blob;
        const form = new FormData();
        form.append('file', blob, 'thumbnail');
        form.append('__RequestVerificationToken', antiforgery());
        await fetch(`api/canvases/${canvasId}/thumbnail`, { method: 'POST', body: form });
    } finally {
        gridArea = null;
        handles.show();
        others.show();
        effects.show();
        badges.show();
    }
}

// ---- Other people --------------------------------------------------------------------------------------------------

const people = new Map<string, CanvasPerson>();
const cursors = new Map<string, { group: Konva.Group; hide: number }>();
const drafts = new Map<string, Konva.Line>();               // their half-drawn strokes, by stroke ID

// Each person keeps the same color for their cursor and half-drawn strokes
const palette = ['#7F77DD', '#D85A30', '#1D9E75', '#378ADD', '#BA7517', '#D4537E'];
function colorOf(personId: string) {
    let hash = 0;
    for (const c of personId) hash = (hash * 31 + c.charCodeAt(0)) | 0;
    return palette[Math.abs(hash) % palette.length];
}

// An arrow pointer like the one on your own screen, its tip at (0, 0)
const POINTER = 'M0 0 L0 17 L4.6 12.6 L7.6 19.4 L10.4 18.2 L7.4 11.6 L13.4 11.6 Z';

// Cursors are drawn in board units, so undo the zoom to keep them the size of a real pointer
const keepScreenSize = (group: Konva.Group) => group.scale({ x: 1 / stage.scaleX(), y: 1 / stage.scaleY() });

function showCursor(personId: string, x: number, y: number) {
    let cursor = cursors.get(personId);
    if (!cursor) {
        const group = new Konva.Group();
        group.add(new Konva.Path({ data: POINTER, fill: colorOf(personId), stroke: '#ffffff', strokeWidth: 1.5, lineJoin: 'round' }));
        group.add(new Konva.Label({ x: 12, y: 18 })
            .add(new Konva.Tag({ fill: colorOf(personId), cornerRadius: 4 }))
            .add(new Konva.Text({ text: people.get(personId)?.name ?? '…', fontSize: 12, padding: 4, fill: '#ffffff' })));
        others.add(group);
        cursor = { group, hide: 0 };
        cursors.set(personId, cursor);
    }
    keepScreenSize(cursor.group);
    cursor.group.position({ x, y }).visible(true);
    // Hide it when they stop moving, so a cursor isn't left sitting on the board
    clearTimeout(cursor.hide);
    cursor.hide = window.setTimeout(() => {
        cursor!.group.visible(false);
        showPointers();
    }, 4000);
    showPointers();
}

// And when you zoom, every cursor already showing
stage.on('scaleXChange', () => cursors.forEach(c => keepScreenSize(c.group)));

// Someone whose cursor is off the screen gets a marker at the edge pointing their way, since the board has no end to
// lose them on. Clicking it goes there.
const pointers = new Map<string, HTMLButtonElement>();
const EDGE_INSET = 24;                                     // from the edge to the marker's middle, leaving room for its arrow

function showPointers() {
    const width = stage.width(), height = stage.height();
    const middle = { x: width / 2, y: height / 2 };
    const transform = stage.getAbsoluteTransform();
    for (const id of new Set([...cursors.keys(), ...pointers.keys()])) {
        const cursor = cursors.get(id);
        const at = cursor && transform.point(cursor.group.position());
        let marker = pointers.get(id);
        if (!cursor || !at || !cursor.group.visible() || (at.x >= 0 && at.y >= 0 && at.x <= width && at.y <= height)) {
            marker?.remove();
            pointers.delete(id);
            continue;
        }

        if (!marker) {
            const name = people.get(id)?.name ?? 'Someone';
            marker = Object.assign(document.createElement('button'), {
                type: 'button', className: 'offscreen-cursor', textContent: [...name][0]?.toUpperCase() ?? '?', title: `Go to ${name}`,
            });
            marker.style.setProperty('--who', colorOf(id));
            marker.addEventListener('click', () => {
                const where = cursors.get(id)?.group.position();
                if (where) lookAt(where);
            });
            container.append(marker);
            pointers.set(id, marker);
        }

        // Where the line from the middle of the box towards them crosses the edge (pulled in a little)
        const dx = at.x - middle.x, dy = at.y - middle.y;
        const reach = Math.min((middle.x - EDGE_INSET) / Math.abs(dx || 1e-9), (middle.y - EDGE_INSET) / Math.abs(dy || 1e-9));
        marker.style.left = `${middle.x + dx * reach}px`;
        marker.style.top = `${middle.y + dy * reach}px`;
        marker.style.setProperty('--angle', `${Math.atan2(dy, dx)}rad`);
    }
}
stage.on('xChange yChange scaleXChange widthChange heightChange', showPointers);

function showPeople(list: CanvasPerson[]) {
    people.clear();
    for (const person of list) people.set(person.id, person);

    // Their cursors go when they leave, and so does following them
    for (const [id, cursor] of cursors)
        if (!people.has(id)) { cursor.group.destroy(); cursors.delete(id); }
    for (const id of views.keys()) if (!people.has(id)) views.delete(id);
    if (following && !people.has(following)) follow(null);
    showPointers();

    // Someone new doesn't know where you're looking yet, for following you
    sendView();

    document.querySelector('[data-here]')!.textContent = `${list.length} here`;
    const box = document.querySelector('[data-people]')!;
    box.replaceChildren(...list.map(person => {
        const el = person.avatarUrl ? Object.assign(document.createElement('img'), { src: person.avatarUrl, alt: '' })
            : Object.assign(document.createElement('span'), { className: 'initial', textContent: person.name[0] });
        if (person.id === live?.me.id) {
            el.title = `${person.name} (you)`;
            return el;
        }
        // Anyone else can be followed
        const button = Object.assign(document.createElement('button'), { type: 'button', title: `Follow ${person.name}` });
        button.dataset.follow = person.id;
        button.style.setProperty('--who', colorOf(person.id));
        button.setAttribute('aria-pressed', String(person.id === following));
        button.append(el);
        return button;
    }));
}

// ---- Following someone ---------------------------------------------------------------------------------------------

// Clicking someone's face at the top follows them, like in Figma: your view goes wherever they pan and zoom, with a
// border in their color around the board, until you pan or zoom yourself, press Escape, or click them again.
const views = new Map<string, { x: number; y: number; width: number; height: number }>();
let following: string | null = null;
let showingTheirs = false;                                 // their view going on yours, which isn't you moving

const followBadge = Object.assign(document.createElement('button'), { type: 'button', className: 'follow-badge', hidden: true });
followBadge.addEventListener('click', () => follow(null));
container.append(followBadge);

function follow(personId: string | null) {
    following = personId && people.has(personId) ? personId : null;
    container.classList.toggle('following', !!following);
    followBadge.hidden = !following;
    document.querySelectorAll<HTMLElement>('[data-follow]').forEach(b => b.setAttribute('aria-pressed', String(b.dataset.follow === following)));
    if (!following) return;
    container.style.setProperty('--who', colorOf(following));
    followBadge.textContent = `Following ${people.get(following)!.name}`;
    followBadge.title = 'Stop following';
    showTheirView();
}

// Fits what they can see into your board, around the same middle, however different the two windows are
function showTheirView() {
    const area = following && views.get(following);
    if (!area) return;
    const scale = clampZoom(Math.min(stage.width() / area.width, stage.height() / area.height));
    showingTheirs = true;
    stage.scale({ x: scale, y: scale });
    stage.position({
        x: stage.width() / 2 - (area.x + area.width / 2) * scale,
        y: stage.height() / 2 - (area.y + area.height / 2) * scale,
    });
    showingTheirs = false;
    viewMoved = true;                                      // so a resize or reload doesn't fit the view over theirs
}

function sendView() {
    const { x, y, width, height } = visibleArea();
    const round = (n: number) => Math.round(n * 10) / 10;
    live?.view(round(x), round(y), round(width), round(height));
}

stage.on('xChange yChange scaleXChange', () => {
    // Panning or zooming yourself stops following
    if (following && !showingTheirs) follow(null);
    sendView();
});
stage.on('widthChange heightChange', () => {
    if (following) showTheirView();
    sendView();
});

document.querySelector('[data-people]')!.addEventListener('click', e => {
    const id = (e.target as HTMLElement).closest<HTMLElement>('[data-follow]')?.dataset.follow;
    if (id) follow(id === following ? null : id);
});
document.addEventListener('keydown', e => { if (e.key === 'Escape' && following) follow(null); });

const status = document.querySelector<HTMLElement>('[data-status]')!;
const statusDot = status.querySelector<HTMLElement>('.live-dot')!;

function showStatus(state: 'live' | 'reconnecting' | 'offline', text?: string) {
    status.className = 'board-status ' + state;
    statusDot.classList.toggle('off', state !== 'live');
    status.querySelector('[data-status-text]')!.textContent = text ?? { live: 'Live', reconnecting: 'Reconnecting…', offline: 'Offline' }[state];
}

// A flash of the dot when someone else changes the board, like the top bar's when the bot changes something
function pulse() {
    statusDot.classList.remove('pulse');
    void statusDot.offsetWidth;
    statusDot.classList.add('pulse');
}

// Everything above exists now, so the tools can be set up
setTool('move');

// ---- Connecting ----------------------------------------------------------------------------------------------------

// Leaving by a link (like the back arrow): tell the server first, or the page you land on (like the canvases list)
// can be drawn before it notices the connection closed, and still count you as here. Half a second at most.
document.addEventListener('click', e => {
    const link = (e.target as HTMLElement).closest<HTMLAnchorElement>('a[href]');
    if (!link || !live || e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
    if (link.target === '_blank' || new URL(link.href).origin !== location.origin) return;
    e.preventDefault();
    Promise.race([live.leave(), new Promise(resolve => setTimeout(resolve, 500))])
        .finally(() => { location.href = link.href; });
});

// The browser's own Back button or closing the tab can't be held up, so just disconnect as the page goes
window.addEventListener('pagehide', () => { live?.stop(); });
// Coming back to a page the browser kept in memory, the connection is gone; load it fresh
window.addEventListener('pageshow', e => { if (e.persisted) location.reload(); });

let live: CanvasLive | null = null;
try {
    live = await connect(canvasId, {
        load: items => {
            load(items);
            if (!viewMoved) fitView();
            // A canvas without a picture yet (like one made before thumbnails) gets one when it's first opened
            if (!container.dataset.hasThumbnail && items.length) thumbnailSoon();
        },
        changed: thumbnailSoon,
        presence: showPeople,
        status: showStatus,
        cursor: showCursor,
        view: (personId, x, y, width, height) => {
            views.set(personId, { x, y, width, height });
            if (personId === following) showTheirView();
        },
        strokeProgress: (personId, strokeId, points) => {
            let line = drafts.get(strokeId);
            if (!line) {
                line = new Konva.Line({ points: [], stroke: colorOf(personId), strokeWidth: 4, lineCap: 'round', lineJoin: 'round', tension: 0.5, opacity: 0.6 });
                others.add(line);
                drafts.set(strokeId, line);
            }
            line.points(line.points().concat(points));
        },
        strokeEnd: (_, strokeId, json) => {
            drafts.get(strokeId)?.destroy();
            drafts.delete(strokeId);
            addItem(strokeId, 'stroke', json);
            pulse();
        },
        itemAdded: (personId, itemId, type, json) => { addItem(itemId, type, json, personId); pulse(); },
        itemMoved: (_, itemId, x, y) => stage.findOne('#' + itemId)?.position({ x, y }),
        // Moves aren't flashed: a drag sends a dozen a second, which would keep it lit
        itemRemoved: (_, itemId) => { const node = stage.findOne('#' + itemId); if (node) removeItem(node); pulse(); },
        moveAsked: (_, actor) => showRolling(actor),
        moveResolved: showMoveResult,
    });
    // Joining showed everyone before `live` was set, so it couldn't tell which face is yours, or send your view
    showPeople([...people.values()]);
} catch {
    // You can still draw, but nothing is saved or shared
    showStatus('offline', "Couldn't connect. Drawings won't be saved.");
}
