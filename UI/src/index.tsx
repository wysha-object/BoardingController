import 'assets/styles/index.scss'
import Checkbox from "components/base/checkbox";
import { getModule, ModRegistrar, ModuleRegistryExtend } from "cs2/modding";
import { Button } from 'cs2/ui';
import { getWaypointCmd, setWaypointCmd } from "hooks/cmd";
import { useTranslate } from "hooks/translate";
import { useState, useEffect, useRef } from "react";
import { Waypoint } from "types";

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.extend("game-ui/game/components/selected-info-panel/selected-info-sections/lines-section/lines-section.tsx", "LineItem", LineItemExtend)
}

const LineItemExtend: ModuleRegistryExtend = (CurrLineItem) => {
    return (props: any) => {

        const [waypoints, setWaypoints] = useState<Waypoint[]>([])
        const prevWaypointsJsonString = useRef<string>("")

        const updateWaypoint = (dependsOn: Promise<void>) => {
            dependsOn.then(() => {
                return getWaypointCmd({ line: props.line.entity })
            }).then((waypoints) => {
                const waypointsJsonString = JSON.stringify(waypoints)
                if (waypointsJsonString !== prevWaypointsJsonString.current) {
                    prevWaypointsJsonString.current = waypointsJsonString
                    setWaypoints(waypoints)
                }
            })
        }

        updateWaypoint(Promise.resolve())

        return (
            <>
                <CurrLineItem {...props} />
                <div className='boarding-controller-root'>
                    {waypoints.map((waypoint) => (
                        <WaypointItem
                            key={waypoint.entity.index}
                            waypoint={waypoint}
                            onUpdate={updateWaypoint}
                        />
                    ))}
                </div>
            </>
        )
    }
}

const WaypointItem = (props: { waypoint: Waypoint, onUpdate: (dependsOn: Promise<void>) => void }) => {
    const { t } = useTranslate()
    return (
        <div style={{ display: 'flex', alignItems: 'center' }}>
            <div >
                {`#${props.waypoint.entity.index}`}
            </div>
            <div style={{ display: 'flex', alignItems: 'center' }}>
                <Button
                    variant='icon'
                    onClick={() => {
                        const promise = setWaypointCmd({ entity: props.waypoint.entity, isLinked: !props.waypoint.isLinked })
                        props.onUpdate(promise)
                    }}
                >
                    <Checkbox
                        isChecked={props.waypoint.isLinked}
                    />
                </Button>
                <div>
                    {t("BoardingController.Waypoint.Linked")}
                </div>
            </div>
        </div>
    )
}

export default register
