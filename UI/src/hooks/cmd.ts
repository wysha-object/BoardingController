import { call } from 'cs2/api'
import { GetWaypointsInput, Waypoint } from 'types'

export async function getWaypointCmd(
  inputValue: GetWaypointsInput,
): Promise<Waypoint[]> {
  return await call('BoardingController', 'GetWaypoints', inputValue)
}
export async function setWaypointCmd(inputValue: Waypoint): Promise<void> {
  return await call('BoardingController', 'SetWaypoint', inputValue)
}