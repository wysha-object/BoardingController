import { Entity } from "cs2/utils";

export enum ToolState {
    Disabled = 0,
    Enabled = 1,
}

export interface Waypoint {
    entity: Entity
    isLinked: boolean
}