import LogoSvg from 'assets/images/logo.svg'

export default function LogoIcon(props: React.SVGProps<SVGSVGElement>) {
  return <LogoSvg
    {...props}
    style={{ width: '36rem', height: '36rem', ...props.style }}
  />
}
