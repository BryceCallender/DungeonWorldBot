// The live connection for a canvas page: who's here, their cursors, and what they draw. Talks to CanvasHub.
//
//   const live = await connect(canvasId, {           // the canvas's short ID, like 'Xb3kQ9'
//       load: items => ...,                           // everything saved, again after reconnecting in case something was missed
//       presence: people => ...,                      // everyone here, including you
//       cursor: (personId, x, y) => ...,
//       view: (personId, x, y, width, height) => ...,   // the part of the board they're looking at, for following
//       strokeProgress: (personId, strokeId, points) => ...,
//       strokeEnd: (personId, strokeId, stroke) => ...,
//       itemAdded: (personId, itemId, type, item) => ...,
//       itemMoved: (personId, itemId, x, y) => ...,
//       itemRemoved: (personId, itemId) => ...,
//       status: state => ...,
//   });
//   live.cursor(x, y); live.strokeEnd(id, line.toObject()); live.addItem(id, 'token', circle.toObject()); ...
//
// Item IDs are made by the page (crypto.randomUUID() works), so an item can be drawn before the server has seen it.
// Finished strokes, added items, moves and removals are saved; cursors and strokes in progress aren't.
import * as signalR from '@microsoft/signalr';

// Shaped like the hub's records (CanvasPerson, SavedItem), which SignalR sends camelCased

export interface CanvasPerson {
    /** Their Discord ID as text, or a guest ID when sign-in is off. */
    id: string;
    name: string;
    avatarUrl: string | null;
    isDungeonMaster: boolean;
}

export type CanvasItemType = 'stroke' | 'token' | 'text' | 'image' | 'shape';

/** An item as the page draws it, like a Konva node's toObject(). */
export type ItemJson = Record<string, unknown>;

export interface SavedItem {
    id: string;
    type: CanvasItemType;
    authorID: string;
    item: ItemJson;
}

export type LiveStatus = 'live' | 'reconnecting' | 'offline';

export interface CanvasHandlers {
    load?: (items: SavedItem[]) => void;
    presence?: (people: CanvasPerson[]) => void;
    cursor?: (personId: string, x: number, y: number) => void;
    view?: (personId: string, x: number, y: number, width: number, height: number) => void;
    /** The points added since last time: [x, y, x, y...]. */
    strokeProgress?: (personId: string, strokeId: string, points: number[]) => void;
    strokeEnd?: (personId: string, strokeId: string, stroke: ItemJson) => void;
    itemAdded?: (personId: string, itemId: string, type: CanvasItemType, item: ItemJson) => void;
    itemMoved?: (personId: string, itemId: string, x: number, y: number) => void;
    itemRemoved?: (personId: string, itemId: string) => void;
    status?: (state: LiveStatus) => void;
    /** You saved a change (a finished stroke, or an item added, moved or removed), like for retaking the thumbnail. */
    changed?: () => void;
}

export interface CanvasLive {
    /** You, as the others see you. */
    me: CanvasPerson;
    connection: signalR.HubConnection;
    cursor(x: number, y: number): void;
    /** The part of the board you're looking at, in board units. */
    view(x: number, y: number, width: number, height: number): void;
    strokeProgress(strokeId: string, points: number[]): void;
    strokeEnd(strokeId: string, stroke: ItemJson): void;
    addItem(itemId: string, type: CanvasItemType, item: ItemJson): void;
    moveItem(itemId: string, x: number, y: number): void;
    removeItem(itemId: string): void;
    stop(): Promise<void>;
    /** Tells the server you've gone, and waits until it has, then disconnects. */
    leave(): Promise<void>;
}

interface JoinResult {
    me: CanvasPerson;
    people: CanvasPerson[];
    items: SavedItem[];
}

// The names CanvasHub calls (ICanvasClient), and the handler each goes to
const hubEvents = {
    PresenceChanged: 'presence',
    Cursor: 'cursor',
    View: 'view',
    StrokeProgress: 'strokeProgress',
    StrokeEnd: 'strokeEnd',
    ItemAdded: 'itemAdded',
    ItemMoved: 'itemMoved',
    ItemRemoved: 'itemRemoved',
} as const satisfies Record<string, keyof CanvasHandlers>;

export async function connect(canvasId: string, on: CanvasHandlers = {}): Promise<CanvasLive> {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl('hubs/canvas')
        // Keeps retrying instead of giving up after four tries, since a session can run for hours
        .withAutomaticReconnect({ nextRetryDelayInMilliseconds: c => Math.min(1000 * 2 ** c.previousRetryCount, 30000) })
        .build();

    for (const [hubName, event] of Object.entries(hubEvents))
        connection.on(hubName, (...args: unknown[]) => (on[event] as ((...a: unknown[]) => void) | undefined)?.(...args));

    // Dropped while reconnecting. Rejoining loads what's saved, but a stroke finished while offline is lost.
    function send(method: string, ...args: unknown[]) {
        if (connection.state === signalR.HubConnectionState.Connected)
            connection.send(method, ...args).catch(() => {});
    }

    // The calls the server saves, which change what the canvas looks like
    function save(method: string, ...args: unknown[]) {
        send(method, ...args);
        on.changed?.();
    }

    const live: CanvasLive = {
        me: null!,
        connection,

        // Cursors move far more often than anyone needs to see, so send at most ~30 a second, and always the latest
        cursor: throttle((x: number, y: number) => send('Cursor', x, y), 33),
        // Panning and zooming change it every frame; anyone following is smooth enough at ~20 a second
        view: throttle((x: number, y: number, width: number, height: number) => send('View', x, y, width, height), 50),
        strokeProgress: (strokeId, points) => send('StrokeProgress', strokeId, points),
        strokeEnd: (strokeId, stroke) => save('StrokeEnd', strokeId, stroke),
        addItem: (itemId, type, item) => save('AddItem', itemId, type, item),
        moveItem: (itemId, x, y) => save('MoveItem', itemId, x, y),
        removeItem: itemId => save('RemoveItem', itemId),
        stop: () => connection.stop(),
        leave: async () => {
            try {
                if (connection.state === signalR.HubConnectionState.Connected) await connection.invoke('Leave');
            } finally {
                await connection.stop();
            }
        },
    };

    async function join() {
        const { me, people, items } = await connection.invoke<JoinResult>('Join', canvasId);
        live.me = me;
        on.load?.(items);
        on.presence?.(people);
        on.status?.('live');
    }

    connection.onreconnecting(() => on.status?.('reconnecting'));
    // A new connection isn't in the canvas's group yet
    connection.onreconnected(() => join());
    connection.onclose(() => on.status?.('offline'));

    await connection.start();
    await join();
    return live;
}

function throttle<A extends unknown[]>(fn: (...args: A) => void, ms: number): (...args: A) => void {
    let last = 0;
    let timer: ReturnType<typeof setTimeout> | null = null;
    let latest: A;
    return (...args) => {
        latest = args;
        const wait = last + ms - Date.now();
        if (wait <= 0) {
            last = Date.now();
            fn(...latest);
        } else if (!timer) {
            timer = setTimeout(() => { timer = null; last = Date.now(); fn(...latest); }, wait);
        }
    };
}
