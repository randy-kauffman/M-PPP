# Playbook: paquete-plugins · sanic_mppp_pkg_zzspiked72

SPIKE D-72 (descartable). Ver `spikes/d72-transaccion-capi/ORDEN.md`. Se arma con:

```
dotnet pack spikes/d72-transaccion-capi/src/Sanic.Mppp.Spike.D72.Paquete/Sanic.Mppp.Spike.D72.Paquete.csproj -c Release
```

## 1. Identidad

```json
{
  "tipo_playbook": "paquete-plugins",
  "version_skill": "0.1.0",
  "proyecto": "2026-001-referencias-planes-pago",
  "inventario": "D-72",
  "fase": 0,
  "entorno_url": "https://org36e60d9d.crm.dynamics.com/",
  "solucion": "sanic_mppp_sol_zzspiked72",
  "publisher": "Sistemas_Abiertos_Nicaragua",
  "prefijo": "sanic",
  "abrev": "mppp",
  "prefijo_opciones": 15946,
  "lcid": 1033
}
```

## 2. Qué se crea

```json
{
  "tipo": "paquete-plugins",
  "nombre": "sanic_mppp_pkg_zzspiked72",
  "uniquename": "sanic_mppp_pkg_zzspiked72",
  "version": "1.0.0",
  "nupkg": "spikes/d72-transaccion-capi/src/Sanic.Mppp.Spike.D72.Paquete/bin/Release/SanicMpppSpikeD72.1.0.0.nupkg",
  "tipos": [
    "Sanic.Mppp.Spike.D72.TransaccionCapiApi"
  ]
}
```

## 3. Precondiciones

- La solución temporal `sanic_mppp_sol_zzspiked72` existe, unmanaged, con el publisher `Sistemas_Abiertos_Nicaragua`.

## 4. Qué NO hace esta herramienta

- No borra el paquete: la limpieza del spike lo borra a mano (Dev, autorizado solo para el spike).
