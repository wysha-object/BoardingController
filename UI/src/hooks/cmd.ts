import { useValue, bindValue, call } from 'cs2/api'
import { Entity } from 'cs2/utils'
import { ToolState, Waypoint } from 'types'

export function useGetToolStateCmd(): ToolState {
  return JSON.parse(useValue(bindValue('BoardingController', 'GetToolState')))
}

export async function setToolStateCmd(inputValue: ToolState): Promise<void> {
  return await call('BoardingController', 'SetToolState', inputValue)
}
export async function getWaypointCmd(inputValue: { line: Entity}): Promise<Waypoint[]> {
  return JSON.parse(await call('BoardingController', 'GetWaypoints', JSON.stringify(inputValue)))
}
export async function setWaypointCmd(inputValue: Waypoint): Promise<void> {
  return await call('BoardingController', 'SetWaypoint', JSON.stringify(inputValue))
}