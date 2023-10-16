import { sendWebsocketMsg } from './request'

export const screenUpdateFull = (names, type) => {
    return sendWebsocketMsg('screen/full', {
        names, type
    });
}
export const screenUpdateRegion = (names) => {
    return sendWebsocketMsg('screen/region', names);
}
export const screenClip = (name, x, y, scale) => {
    return sendWebsocketMsg('screen/clip', {
        name,
        clip: {
            x, y, scale
        }
    });
}