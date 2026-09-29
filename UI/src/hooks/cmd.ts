import { useValue, bindValue, call } from 'cs2/api'
import { Entity } from 'cs2/utils'
import { Waypoint } from 'types'

export async function getWaypointCmd(inputValue: { line: Entity}): Promise<Waypoint[]> {
  return JSON.parse(await call('BoardingController', 'GetWaypoints', JSON.stringify(inputValue)))
}
export async function setWaypointCmd(inputValue: Waypoint): Promise<void> {
  return await call('BoardingController', 'SetWaypoint', JSON.stringify(inputValue))
}