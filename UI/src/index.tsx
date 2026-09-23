import LogoIcon from "components/icon/logo-icon";
import { ModRegistrar } from "cs2/modding";
import { Button, Tooltip } from "cs2/ui";
import { useTranslate } from "hooks/translate";

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.append('GameTopLeft', () => <App />)
}

function App() {
    const { t } = useTranslate()

    const floatingButtonClickHandler = () => {
        throw new Error()
    }

    return (
        <div id='boarding-controller-root'>
            <Tooltip tooltip={t('BoardingController')}>
                <Button variant='floating' onSelect={floatingButtonClickHandler}>
                    <LogoIcon />
                </Button>
            </Tooltip>
        </div>
    )
}

export default register
