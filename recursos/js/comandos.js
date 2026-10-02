/*
 * WR - MPPP - JS - Comandos
 *
 * Los botones de la barra de comandos de Fila y Solicitud (inventario 12.5).
 * Son comandos CLASICOS con web resource JavaScript (DA-02, corregida el
 * 2026-09-22): un comando moderno no se puede crear por API.
 *
 * LA LOGICA NO VIVE ACA (DA-07). Un boton solo escribe `sanic_estado` /
 * `sanic_estadoprocesamiento`. Que la transicion sea valida, quien puede
 * hacerla y la segregacion de funciones los decide el plugin de transicion
 * (`03` seccion 4). Un usuario que llame al Web API sin pasar por la app
 * encuentra las mismas reglas.
 *
 * Accion masiva: el ribbon pasa `SelectedControlSelectedItemIds`, igual que
 * Power Fx pasaria `Self.Selected.AllItems`. Una tanda puede fallar en unas
 * filas y en otras no (por ejemplo, el supervisor intenta aprobar una fila que
 * el mismo digito): se informa cuantas se aplicaron y cuales no y por que, y
 * nunca se deja una fila a medias.
 *
 * EL DIALOGO DE MOTIVO (DA-03, corregida el 2026-09-23). Los comandos que piden
 * un texto abren el formulario de CREACION RAPIDA de
 * `sanic_mppp_tbl_motivoaccion` con `openForm` y `useQuickCreateForm`.
 *
 * NO es una pagina custom, y no por gusto: `navigateTo` a una pagina custom
 * resuelve el promise SIN NINGUN VALOR al cerrar el dialogo, asi que la pagina
 * no tendria como devolver el motivo; y su `pageInput` solo admite un
 * `recordId` que debe ser un GUID, asi que tampoco podria recibir las 25 filas
 * seleccionadas.
 *
 * Y tampoco es el formulario PRINCIPAL en un modal, que es lo que hacia hasta el
 * 2026-09-23 con `pageType: "entityrecord"`. Esa variante funcionaba, pero traia
 * el formulario entero adentro del dialogo: barra de comandos, titulo de
 * registro, pestaña y la barra de Copilot, para pedir una linea de texto. La
 * creacion rapida devuelve `savedEntityReference` igual (ver `pedirMotivo`).
 *
 * El registro de motivo es TRANSPORTE, no dato de negocio: se lee y se borra.
 * El texto queda en `sanic_mensaje` de cada fila (o en `sanic_motivorevision`
 * de la solicitud, que el plugin pasa a la Bitacora).
 */
var Sanic = Sanic || {};
Sanic.Mppp = Sanic.Mppp || {};

Sanic.Mppp.Comandos = (function () {
    "use strict";

    // Valores reales del entorno (leidos de la metadata el 2026-09-22), no de memoria.
    //
    // OJO con el nombre de la tabla. `Xrm.WebApi.updateRecord` pide el nombre
    // LOGICO (`sanic_mppp_tbl_fila`), no el del conjunto de entidades
    // (`sanic_mppp_tbl_filas`, con ese). Hasta el 2026-09-23 esto decia el del
    // conjunto y TODOS los comandos que cambian estado fallaban con
    // "The entity 'sanic_mppp_tbl_filas' cannot be found", un error que solo
    // aparece al apretar el boton: al guardar el web resource no lo ve nadie.
    var FILA = {
        tabla: "sanic_mppp_tbl_fila",
        columna: "sanic_estado",
        mensaje: "sanic_mensaje",          // lo lee el cliente
        notaInterna: "sanic_notainterna",  // lo lee el ejecutivo; no sale del banco
        VALIDADA: 159460003,
        DIGITADA: 159460004,
        APROBADA: 159460005,
        RECHAZADA_AS400: 159460006,
        ANULADA: 159460007
    };
    var SOLICITUD = {
        tabla: "sanic_mppp_tbl_solicitud",
        columna: "sanic_estadoprocesamiento",
        CERRADA: 159460007,
        DESCARTADA: 159460003
    };
    var MOTIVO = { tabla: "sanic_mppp_tbl_motivoaccion", columna: "sanic_motivo" };

    /*
     * DESDE QUE ESTADO SE PUEDE HACER CADA ACCION. Unica copia en el cliente.
     *
     * No contradice DA-07 ("la logica no vive aca") por una asimetria que hay que
     * tener presente: **esto solo OCULTA**. La autoridad sigue siendo
     * `Dominio/TransicionesDeFila.cs`, que corre en el servidor y rechaza igual.
     *   - si aca se muestra de mas, el plugin rechaza y no pasa nada grave;
     *   - si aca se oculta de mas, el usuario no puede trabajar y no sabe por que.
     * Por eso, ANTE LA DUDA SE MUESTRA (ver `algunaAdmite`).
     *
     * Este mapa es una COPIA de la tabla del dominio y puede divergir de ella sin
     * que nada se queje: ninguna prueba unitaria puede cruzar C# con JavaScript.
     * Lo verifica `herramientas/pruebas/verificar_transiciones.py`, que lee las dos
     * y las compara. Si se agrega una transicion al dominio, hay que tocar esto.
     */
    var TRANSICIONES_VALIDAS = {
        digitada: [FILA.VALIDADA],
        aprobar: [FILA.DIGITADA],
        devolver: [FILA.DIGITADA],
        rechazadaas400: [FILA.VALIDADA, FILA.DIGITADA],
        anular: [FILA.VALIDADA, FILA.DIGITADA]
    };

    // En el XML del ribbon la plataforma sustituye por idioma; acá no. Por eso
    // cada texto lleva su version en los dos idiomas del proyecto.
    var TEXTOS = {
        1033: {
            confirmar: "Apply \"{accion}\" to {n} selected row(s)?",
            titulo: "Confirm",
            si: "Apply",
            no: "Cancel",
            listo: "{ok} of {n} row(s) updated.",
            conFallas: "{ok} of {n} row(s) updated. {mal} could not be updated:",
            ninguna: "No rows were selected.",
            cerrar: "OK",
            sinMotivo: "No reason was entered. Nothing was changed.",
            progreso: "Applying \"{accion}\"\u2026 {hechas} of {n}",
            descargando: "Preparing the Excel file\u2026",
            errorDescarga: "The file could not be prepared:",
            // DN-04: carga y cambio de estado masivos de catalogos. Los textos del
            // resumen son los que fija el contrato (DIRECTRICES-NUEVAS, DN-04).
            leyendo: "Reading the file\u2026",
            validando: "Validating and loading the file\u2026 it may take up to a minute",
            preparando: "Preparing the result\u2026",
            generandoPlantilla: "Generating the template\u2026",
            extensionInvalida: "The file must be an Excel workbook (.xlsx). Nothing was sent.",
            sinArchivo: "The server did not return a file.",
            cargaGrabada: "Load finished: {clientes} clients, {planes} plans, {correos} emails and {autorizaciones} authorizations created.",
            estadoGrabado: "Status change finished: {activados} activated, {desactivados} deactivated and {sincambios} unchanged.",
            conAdvertencias: "{advertencias} rows with warnings: check the result file.",
            noGrabado: "Nothing was saved: {errores} rows with errors. Check the Result column of the downloaded file, fix it and upload it again."
        },
        3082: {
            confirmar: "¿Aplicar \"{accion}\" a {n} fila(s) seleccionada(s)?",
            titulo: "Confirmar",
            si: "Aplicar",
            no: "Cancelar",
            listo: "{ok} de {n} fila(s) actualizada(s).",
            conFallas: "{ok} de {n} fila(s) actualizada(s). {mal} no se pudieron actualizar:",
            ninguna: "No hay ninguna fila seleccionada.",
            cerrar: "Aceptar",
            sinMotivo: "No se escribio ningun motivo. No se cambio nada.",
            progreso: "Aplicando \"{accion}\"\u2026 {hechas} de {n}",
            descargando: "Preparando el archivo de Excel\u2026",
            errorDescarga: "No se pudo preparar el archivo:",
            leyendo: "Leyendo el archivo\u2026",
            validando: "Validando y cargando el archivo\u2026 puede tardar hasta un minuto",
            preparando: "Preparando el resultado\u2026",
            generandoPlantilla: "Generando la plantilla\u2026",
            extensionInvalida: "El archivo tiene que ser un libro de Excel (.xlsx). No se envi\u00f3 nada.",
            sinArchivo: "El servidor no devolvi\u00f3 ning\u00fan archivo.",
            cargaGrabada: "Carga terminada: {clientes} clientes, {planes} planes, {correos} correos y {autorizaciones} autorizaciones creados.",
            estadoGrabado: "Cambio de estado terminado: {activados} activados, {desactivados} desactivados y {sincambios} sin cambios.",
            conAdvertencias: "{advertencias} filas con advertencias: revis\u00e1 el archivo de resultado.",
            noGrabado: "No se grab\u00f3 nada: {errores} filas con errores. Revis\u00e1 la columna Resultado del archivo que se descarg\u00f3, correg\u00ed y volv\u00e9 a subirlo."
        }
    };

    function idioma() {
        try {
            var lcid = Xrm.Utility.getGlobalContext().userSettings.languageId;
            return TEXTOS[lcid] ? TEXTOS[lcid] : TEXTOS[1033];
        } catch (e) {
            return TEXTOS[1033];
        }
    }

    function armar(plantilla, valores) {
        var texto = plantilla;
        for (var clave in valores) {
            if (Object.prototype.hasOwnProperty.call(valores, clave)) {
                texto = texto.split("{" + clave + "}").join(valores[clave]);
            }
        }
        return texto;
    }

    // Un id puede llegar con llaves segun desde donde se invoque el comando.
    function limpiarId(id) {
        return String(id).replace("{", "").replace("}", "");
    }

    function refrescar(control) {
        try {
            if (control && typeof control.refresh === "function") {
                control.refresh();
            }
        } catch (e) {
            // Que no se pueda refrescar la grilla no invalida lo que ya se escribio.
        }
    }

    /*
     * Aplica el mismo cambio a cada fila con UNA PETICION POR FILA, en paralelo.
     *
     * Una peticion por fila y no un lote, a proposito: una fila que el plugin
     * rechaza no tiene por que arrastrar a las demas, y asi se informa el motivo
     * de cada rechazo por separado.
     *
     * EN PARALELO, tambien a proposito: con 100 filas la tanda tarda unos 10
     * segundos asi, y encadenadas tardaria varias veces mas con la pantalla
     * esperando. Hasta el 2026-09-23 este comentario decia "UNA POR UNA", que
     * suena secuencial y no lo es; sobre esa frase se tomaron decisiones
     * equivocadas (se copio incluso a otra herramienta del proyecto).
     *
     * Que las filas viajen concurrentes tiene una consecuencia del lado del
     * servidor: dos transiciones de la MISMA Solicitud podrian leer su estado a
     * medias. Eso NO se arregla aca sino en el plugin, que toma el bloqueo de la
     * Solicitud antes de mirar sus filas (`PlomeriaDeFila.TomarElBloqueoDeLaSolicitud`).
     *
     * `alAvanzar` recibe cuantas terminaron, para el indicador de progreso.
     * Devuelve el detalle de las que fallaron.
     */
    function aplicar(tabla, ids, cambio, alAvanzar) {
        var hechas = 0;
        function avanzar() {
            hechas = hechas + 1;
            if (alAvanzar) {
                alAvanzar(hechas);
            }
        }
        var resultados = ids.map(function (id) {
            return Xrm.WebApi.updateRecord(tabla, limpiarId(id), cambio).then(
                function () { avanzar(); return null; },
                function (error) { avanzar(); return (error && error.message) || String(error); }
            );
        });
        return Promise.all(resultados).then(function (fallas) {
            return fallas.filter(function (f) { return f !== null; });
        });
    }

    /*
     * Corre `tarea()` con el indicador de progreso arriba, y lo CIERRA PASE LO QUE PASE.
     *
     * Learn avisa en negrita sobre `showProgressIndicator`: "el dialogo de progreso
     * BLOQUEA LA INTERFAZ hasta que se cierra con closeProgressIndicator". O sea que
     * un error que escape sin cerrarlo deja al usuario con la aplicacion muerta y sin
     * mas salida que recargar la pagina. Por eso el cierre va en los DOS caminos del
     * `then`, y por eso `mostrar` y `cerrar` se trapean: que el indicador no ande no
     * puede impedir que la operacion siga ni que se libere la pantalla.
     *
     * Con 100 filas la tanda tarda unos 10 segundos (medido en Dev el 2026-09-23), y
     * sin esto el usuario no tiene ninguna senal de que algo esta pasando.
     */
    function conProgreso(mensajeInicial, tarea) {
        mostrarProgreso(mensajeInicial);
        var listo = false;
        function cerrar() {
            if (!listo) {
                listo = true;
                cerrarProgreso();
            }
        }
        return tarea().then(
            function (valor) { cerrar(); return valor; },
            function (error) { cerrar(); throw error; }
        );
    }

    function mostrarProgreso(mensaje) {
        try {
            Xrm.Utility.showProgressIndicator(mensaje);
        } catch (e) {
            // Sin indicador se trabaja igual; sin poder cerrarlo, no.
        }
    }

    function cerrarProgreso() {
        try {
            Xrm.Utility.closeProgressIndicator();
        } catch (e) {
            // `closeProgressIndicator` no hace nada si no hay dialogo abierto (Learn).
        }
    }

    function ejecutar(nombreAccion, tabla, cambio, ids, control) {
        var t = idioma();
        if (!ids || !ids.length) {
            return Xrm.Navigation.openAlertDialog({ text: t.ninguna, confirmButtonLabel: t.cerrar });
        }
        var total = ids.length;
        return Xrm.Navigation.openConfirmDialog(
            {
                title: t.titulo,
                text: armar(t.confirmar, { accion: nombreAccion, n: total }),
                confirmButtonLabel: t.si,
                cancelButtonLabel: t.no
            }
        ).then(function (respuesta) {
            if (!respuesta || !respuesta.confirmed) {
                return null;
            }
            return conProgreso(
                armar(t.progreso, { accion: nombreAccion, hechas: 0, n: total }),
                function () {
                    return aplicar(tabla, ids, cambio, function (hechas) {
                        mostrarProgreso(armar(t.progreso, { accion: nombreAccion, hechas: hechas, n: total }));
                    });
                }
            ).then(function (fallas) {
                refrescar(control);
                var ok = total - fallas.length;
                var texto = fallas.length
                    ? armar(t.conFallas, { ok: ok, n: total, mal: fallas.length }) + "\n\n" + fallas.join("\n")
                    : armar(t.listo, { ok: ok, n: total });
                return Xrm.Navigation.openAlertDialog({ text: texto, confirmButtonLabel: t.cerrar });
            });
        });
    }

    /*
     * Los estados de las filas SELECCIONADAS, leidos de la grilla que ya esta en
     * pantalla: `sanic_estado` es una columna de la vista, asi que el dato ya viajo
     * y no hace falta ir al servidor. Eso importa porque una EnableRule se evalua en
     * CADA cambio de seleccion: una consulta ahi haria la grilla inusable.
     *
     * `null` si no se pudo averiguar (otra vista, la grilla todavia cargando, una
     * version distinta del control). Quien llama decide, y decide MOSTRAR.
     */
    /* `getSelectedRows()` devuelve una Collection, no un array. `forEach` es suyo y
     * no siempre recorre; `getAll()` e indices son los otros dos caminos. Se prueban
     * los tres porque cual funciona depende del tipo de grilla. */
    function filasDeLaGrilla(grid) {
        var coleccion = grid.getSelectedRows();

        if (coleccion && typeof coleccion.getAll === "function") {
            var todas = coleccion.getAll();
            if (todas && todas.length) { return todas; }
        }

        var cuantas = (coleccion && typeof coleccion.getLength === "function")
            ? coleccion.getLength() : 0;
        var filas = [];
        for (var i = 0; i < cuantas; i++) {
            filas.push(coleccion.get(i));
        }
        return filas;
    }

    /* El estado de UNA fila. Cada paso devuelve `undefined` en cuanto algo falta, en
     * vez de reventar: quien llama ya trata `undefined` como "no se pudo leer". */
    function estadoDeLaFila(fila) {
        var datos = fila && (fila.data || (typeof fila.getData === "function" ? fila.getData() : null));
        var registro = datos && (datos.entity || (typeof datos.getEntity === "function" ? datos.getEntity() : null));

        /* Tres nombres para la misma coleccion segun version y tipo de grilla. La
         * documentacion solo bendice `columns`, y solo en grillas editables. */
        var columnas = null;
        if (registro) {
            if (typeof registro.getAttributes === "function") {
                columnas = registro.getAttributes();
            } else if (registro.attributes) {
                columnas = registro.attributes;
            } else if (registro.columns) {
                columnas = registro.columns;
            }
        }

        if (!columnas || typeof columnas.get !== "function") { return undefined; }

        var columna = columnas.get(FILA.columna);
        return (columna && typeof columna.getValue === "function") ? columna.getValue() : undefined;
    }

    function estadosSeleccionados(control) {
        try {
            var filas = filasDeLaGrilla(control.getGrid());
            var estados = [];
            for (var i = 0; i < filas.length; i++) {
                var estado = estadoDeLaFila(filas[i]);
                if (estado !== undefined && estado !== null) { estados.push(estado); }
            }
            return estados.length ? estados : null;
        } catch (e) {
            // No poder leer la grilla no es motivo para ocultar un boton: ver `algunaAdmite`.
            return null;
        }
    }

    /*
     * `true` si AL MENOS UNA de las filas seleccionadas admite la accion.
     *
     * "Al menos una" y no "todas", a proposito: trabajar en lote es lo normal acá, y
     * exigir que las 100 esten en el estado justo obligaria a depurar la seleccion a
     * mano. Las que no correspondan las rechaza el plugin y el resumen final las
     * nombra una por una, que es informacion util, no ruido.
     *
     * Si no se pudo leer el estado, devuelve `true`: ocultar un boton que el usuario
     * necesita es peor que mostrarle uno que el servidor va a rechazar.
     */
    function algunaAdmite(accion, control) {
        var estados = estadosSeleccionados(control);
        if (estados === null) { return true; }
        var validos = TRANSICIONES_VALIDAS[accion] || [];
        return estados.some(function (estado) { return validos.indexOf(estado) >= 0; });
    }

    function cambioDe(config, valor) {
        var cambio = {};
        cambio[config.columna] = valor;
        return cambio;
    }

    /*
     * Abre el dialogo de motivo y devuelve el texto, o null si el usuario
     * cancelo.
     *
     * CREACION RAPIDA, no el formulario principal en un modal (corregido el
     * 2026-09-23). Antes se usaba `navigateTo` con `pageType: "entityrecord"`,
     * que mete el formulario PRINCIPAL completo dentro del dialogo: barra de
     * comandos (Guardar, Nuevo, Flujo), titulo de registro, pestaña y la barra
     * de Copilot. Para pedir una linea de texto.
     *
     * El motivo por el que se habia descartado la creacion rapida era que "solo
     * `entityrecord` devuelve valor". Eso vale contra una PAGINA CUSTOM, no
     * contra la creacion rapida: Learn dice textual que el callback de exito de
     * `openForm` "se ejecuta solo cuando se guarda un registro en un formulario
     * de creacion rapida abierto con openForm", y ese callback recibe
     * `savedEntityReference`.
     *
     * LO QUE SE PIERDE: `openForm` no admite un titulo propio, asi que el
     * contexto ("que accion, sobre cuantas filas") ya no va en el encabezado del
     * dialogo. El usuario acaba de apretar ese boton, y el resumen final vuelve
     * a nombrar la accion y la cantidad.
     *
     * LO QUE NO ESTA VERIFICADO: que hace el promise si el usuario CANCELA.
     * Learn solo documenta que el callback de exito corre al guardar. Por eso
     * hay un manejador de rechazo que devuelve null, y `pedirMotivo` se llama
     * ANTES de abrir cualquier indicador de progreso: si el promise no se
     * resolviera nunca, no queda nada bloqueando la pantalla.
     */
    function pedirMotivo() {
        return Xrm.Navigation.openForm({
            entityName: MOTIVO.tabla,
            useQuickCreateForm: true
        }).then(function (resultado) {
            var ref = resultado && resultado.savedEntityReference && resultado.savedEntityReference[0];
            if (!ref) {
                return null;   // cerro el dialogo sin guardar
            }
            return Xrm.WebApi.retrieveRecord(MOTIVO.tabla, ref.id, "?$select=" + MOTIVO.columna)
                .then(function (registro) {
                    var texto = registro[MOTIVO.columna];
                    // El registro es transporte: se borra. Que el borrado
                    // falle no invalida el motivo ya leido.
                    return Xrm.WebApi.deleteRecord(MOTIVO.tabla, ref.id).then(
                        function () { return texto; },
                        function () { return texto; }
                    );
                });
        }, function () {
            return null;   // cancelo, o el dialogo no se pudo abrir: no se toca nada
        });
    }

    /*
     * Igual que `ejecutar`, pero el texto lo pide el dialogo en vez de una
     * confirmacion. Sin motivo no se toca NADA.
     */
    function ejecutarConMotivo(nombreAccion, tabla, columnaMensaje, cambioBase, ids, control) {
        var t = idioma();
        if (!ids || !ids.length) {
            return Xrm.Navigation.openAlertDialog({ text: t.ninguna, confirmButtonLabel: t.cerrar });
        }
        var total = ids.length;
        return pedirMotivo().then(function (motivo) {
            if (!motivo) {
                return motivo === null
                    ? null
                    : Xrm.Navigation.openAlertDialog({ text: t.sinMotivo, confirmButtonLabel: t.cerrar });
            }
            var cambio = {};
            for (var clave in cambioBase) {
                if (Object.prototype.hasOwnProperty.call(cambioBase, clave)) {
                    cambio[clave] = cambioBase[clave];
                }
            }
            cambio[columnaMensaje] = motivo;
            return conProgreso(
                armar(t.progreso, { accion: nombreAccion, hechas: 0, n: total }),
                function () {
                    return aplicar(tabla, ids, cambio, function (hechas) {
                        mostrarProgreso(armar(t.progreso, { accion: nombreAccion, hechas: hechas, n: total }));
                    });
                }
            ).then(function (fallas) {
                refrescar(control);
                var ok = total - fallas.length;
                var texto = fallas.length
                    ? armar(t.conFallas, { ok: ok, n: total, mal: fallas.length }) + "\n\n" + fallas.join("\n")
                    : armar(t.listo, { ok: ok, n: total });
                return Xrm.Navigation.openAlertDialog({ text: texto, confirmButtonLabel: t.cerrar });
            });
        });
    }

    /*
     * "Descargar para digitar" (DN-02, `diseno/DIRECTRICES-NUEVAS.md`). NO usa
     * `ejecutar`/`aplicar`: esas dos hacen "una peticion POR FILA" para escribir
     * `sanic_estado`; esto es UNA sola llamada a la Custom API
     * `sanic_mppp_capi_exportarparadigitar`, que devuelve un `.xlsx` armado en
     * el servidor (C# + Open XML SDK, DN-02: "no JavaScript: un .xlsx a mano en
     * el navegador es fragil y no se agregan librerias al cliente").
     *
     * `filaids` es StringArray (calza con `SelectedControlSelectedItemIds` sin
     * JSON de por medio, ver el XML doc de `ExportarParaDigitarApi`).
     * `parameterTypes.filaids` usa `structuralProperty: 4` (Collection, Learn
     * "Xrm.WebApi.online.execute") con `typeName: "Edm.String"`: el tipo del
     * ELEMENTO, no de la coleccion.
     *
     * `operationType: 0` (Action/POST), NO 1 (Function/GET): decision del
     * aprobador, 2026-09-26. Como Function, TODA llamada era un GET con los
     * `filaid` en la query string, y la medicion real (BP-PP-053) encontro un
     * HTTP 414 a las ~700 filas — techo de plataforma que ningun parametro
     * mueve. Como Action, los ids viajan en el CUERPO del POST: no hay techo
     * de largo de URL, y el unico limite que queda es el presupuesto de 2
     * minutos que el propio plugin vigila (`PresupuestoDeExportacionAgotandoseException`).
     * La Custom API se borro y se recreo en Dev con `esfuncion: false`
     * (`isfunction` es inmutable una vez guardada, Learn "CustomAPI tables"):
     * no alcanzaba con cambiar este archivo solo.
     */
    function pedidoExportarParaDigitar(ids) {
        return {
            filaids: ids,
            getMetadata: function () {
                return {
                    boundParameter: null,
                    parameterTypes: {
                        filaids: { typeName: "Edm.String", structuralProperty: 4 }
                    },
                    operationType: 0, // Action (Learn: 0 Action, 1 Function, 2 CRUD)
                    operationName: "sanic_mppp_capi_exportarparadigitar"
                };
            }
        };
    }

    /*
     * Nombre del archivo con fecha y hora locales, para que dos descargas del
     * mismo dia no se pisen en la carpeta de Descargas de quien lo baja.
     */
    function nombreDeArchivoExportacion() {
        var ahora = new Date();
        function dosDigitos(n) { return (n < 10 ? "0" : "") + n; }
        var sufijo = ahora.getFullYear().toString() +
            dosDigitos(ahora.getMonth() + 1) + dosDigitos(ahora.getDate()) + "-" +
            dosDigitos(ahora.getHours()) + dosDigitos(ahora.getMinutes()) + dosDigitos(ahora.getSeconds());
        return "MPPP-para-digitar-" + sufijo + ".xlsx";
    }

    /*
     * Base64 -> KB, para `fileSize` de `Xrm.Navigation.openFile` (Learn: numero
     * en KB, no en bytes). Se redondea PARA ARRIBA: mejor declarar un poco de
     * mas que uno de menos.
     */
    function tamanioEnKbDesdeBase64(base64) {
        var bytesAproximados = Math.ceil((base64 || "").length * 3 / 4);
        return Math.max(1, Math.ceil(bytesAproximados / 1024));
    }

    /*
     * Descarga un `.xlsx` que viene en base64. Una sola copia para "Descargar para
     * digitar" (DN-02) y para los menus de catalogos (DN-04): mismo
     * `Xrm.Navigation.openFile` con `openMode: 2` (guardar), ya probado.
     */
    function abrirXlsx(base64, nombre) {
        return Xrm.Navigation.openFile(
            {
                fileContent: base64,
                fileName: nombre,
                fileSize: tamanioEnKbDesdeBase64(base64),
                mimeType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            },
            { openMode: 2 } // 2 = guardar (Learn, "openFile")
        );
    }

    function descargarParaDigitar(ids, control) {
        var t = idioma();
        if (!ids || !ids.length) {
            return Xrm.Navigation.openAlertDialog({ text: t.ninguna, confirmButtonLabel: t.cerrar });
        }
        var idsLimpios = ids.map(limpiarId);
        return conProgreso(t.descargando, function () {
            return Xrm.WebApi.online.execute(pedidoExportarParaDigitar(idsLimpios)).then(function (respuesta) {
                return respuesta.json();
            });
        }).then(function (resultado) {
            return abrirXlsx(resultado.archivo, nombreDeArchivoExportacion());
        }, function (error) {
            // Los dos rechazos de negocio (FilasNoEncontradasException,
            // PresupuestoDeExportacionAgotandoseException) llegan con su mensaje
            // TAL CUAL en `error.message` (decision del aprobador, ver el XML doc
            // de ExportarParaDigitarApi): se muestra sin envolver, es lo que el
            // ejecutivo necesita leer para actuar.
            var texto = (error && error.message) || String(error);
            return Xrm.Navigation.openAlertDialog({
                text: t.errorDescarga + "\n\n" + texto,
                confirmButtonLabel: t.cerrar
            });
        });
    }

    /*
     * --- Carga masiva y cambio de estado masivo de catalogos (DN-04) ---
     *
     * Contrato: `diseno/DIRECTRICES-NUEVAS.md`, DN-04, "Contrato del JS". Igual que
     * "Descargar para digitar", NO usa `ejecutar`/`aplicar`: es UNA llamada a una
     * Custom API (Action, unbound) que lee, valida y graba todo el archivo en el
     * servidor, todo o nada. El JS solo lleva el archivo, muestra el resultado y
     * refresca. La logica de negocio vive en el plugin (DA-07).
     */
    var ROL_ADMINISTRADOR_DE_PLANES = "SR - MPPP - Administrador de planes";

    var CATALOGOS = {
        carga: {
            api: "sanic_mppp_capi_cargarcatalogos",
            plantilla: "CargaCatalogos.xlsx",
            resumen: "cargaGrabada"
        },
        estado: {
            api: "sanic_mppp_capi_cambiarestadocatalogos",
            plantilla: "ActivarDesactivar.xlsx",
            resumen: "estadoGrabado"
        }
    };

    /*
     * La Custom API como Action, mismo patron que `pedidoExportarParaDigitar`.
     * Los parametros son opcionales (`archivo` o `plantilla`), asi que solo se
     * declaran en `parameterTypes` los que realmente viajan: no se manda un
     * parametro sin valor. `structuralProperty: 1` = tipo primitivo (Learn,
     * "Xrm.WebApi.online.execute"); el 4 de `filaids` era una coleccion.
     */
    function pedidoCatalogos(config, parametros) {
        var pedido = {};
        var tipos = {};
        if (parametros.archivo !== undefined) {
            pedido.archivo = parametros.archivo;
            tipos.archivo = { typeName: "Edm.String", structuralProperty: 1 };
        }
        if (parametros.plantilla !== undefined) {
            pedido.plantilla = parametros.plantilla;
            tipos.plantilla = { typeName: "Edm.Boolean", structuralProperty: 1 };
        }
        pedido.getMetadata = function () {
            return {
                boundParameter: null,
                parameterTypes: tipos,
                operationType: 0, // Action (Learn: 0 Action, 1 Function, 2 CRUD)
                operationName: config.api
            };
        };
        return pedido;
    }

    function mensajeDeError(error) {
        // Los MPPP-CAR-00x llegan con su texto en `error.message` y se muestran TAL
        // CUAL (DN-04): el texto ya dice que paso y que hacer.
        return (error && error.message) || String(error);
    }

    function avisarError(error) {
        return Xrm.Navigation.openAlertDialog({
            text: mensajeDeError(error),
            confirmButtonLabel: idioma().cerrar
        });
    }

    /* El archivo entero en base64, SIN el prefijo `data:...;base64,` que agrega el FileReader. */
    function archivoABase64(archivo) {
        return new Promise(function (resolver, rechazar) {
            var lector = new FileReader();
            lector.onload = function () {
                var url = String(lector.result || "");
                resolver(url.substring(url.indexOf(",") + 1));
            };
            lector.onerror = function () {
                rechazar(lector.error || new Error(String(archivo && archivo.name)));
            };
            lector.readAsDataURL(archivo);
        });
    }

    // `<nombre original sin extension>-resultado.xlsx`. La extension ya se validó.
    function nombreDelResultado(nombreOriginal) {
        return String(nombreOriginal).replace(/\.xlsx$/i, "") + "-resultado.xlsx";
    }

    /*
     * El selector que quedo de una llamada anterior. Si el navegador no dispara
     * `cancel` (los viejos no lo hacen), el `input` quedaria en el documento; se
     * retira al abrir el siguiente para que nunca se acumulen.
     */
    var selectorPendiente = null;

    function retirarSelector() {
        if (selectorPendiente && selectorPendiente.parentNode) {
            selectorPendiente.parentNode.removeChild(selectorPendiente);
        }
        selectorPendiente = null;
    }

    /*
     * Abre el selector de archivo. SINCRONICO a proposito: el navegador solo
     * permite `input.click()` dentro del gesto del usuario, y un `await` o un
     * `then` antes lo deja fuera del gesto (indicacion del arquitecto, DN-04). Por
     * eso se llama directo desde la funcion que invoca el ribbon, sin nada en medio.
     *
     * Se agrega al `document` porque en algunos navegadores un `input` suelto no
     * dispara `change`, y se saca al terminar. Cancelar no hace nada: no hay promesa
     * que quede colgada, solo un manejador que nunca se llama.
     *
     * Mientras el selector esta abierto NO hay indicador (DN-04): arranca cuando llega
     * el archivo.
     */
    function elegirArchivo(alElegir) {
        retirarSelector();
        var entrada = document.createElement("input");
        entrada.type = "file";
        entrada.accept = ".xlsx";
        entrada.style.display = "none";
        entrada.addEventListener("change", function () {
            var archivo = entrada.files && entrada.files[0];
            if (entrada === selectorPendiente) { retirarSelector(); }
            if (archivo) { alElegir(archivo); }
        });
        entrada.addEventListener("cancel", function () {
            if (entrada === selectorPendiente) { retirarSelector(); }
        });
        selectorPendiente = entrada;
        document.body.appendChild(entrada);
        entrada.click();
    }

    function textoDelResumen(t, config, salida) {
        var n = function (valor) { return valor || 0; };
        if (!salida.grabado) {
            return armar(t.noGrabado, { errores: n(salida.errores) });
        }
        var texto = config.resumen === "cargaGrabada"
            ? armar(t.cargaGrabada, {
                clientes: n(salida.clientescreados),
                planes: n(salida.planescreados),
                correos: n(salida.correoscreados),
                autorizaciones: n(salida.autorizacionescreadas)
            })
            : armar(t.estadoGrabado, {
                activados: n(salida.activados),
                desactivados: n(salida.desactivados),
                sincambios: n(salida.sincambios)
            });
        if (salida.advertencias > 0) {
            texto = texto + " " + armar(t.conAdvertencias, { advertencias: salida.advertencias });
        }
        return texto;
    }

    /*
     * Lo que pasa DESPUES de elegir el archivo: indicador por etapa (un mensaje por
     * etapa, no un porcentaje: DN-04 decision 10), la Custom API, la descarga del
     * resultado, el resumen y el refresco de la grilla.
     *
     * Si la API falla (los MPPP-CAR) no hay Excel de resultado: se muestra el texto
     * del error y nada mas. Si falla SOLO la descarga, la carga ya esta grabada: no
     * se esconde el resumen detras de un error de descarga; se muestran los dos.
     */
    function procesarArchivo(config, archivo, control) {
        var t = idioma();
        return conProgreso(t.leyendo, function () {
            return archivoABase64(archivo).then(function (base64) {
                mostrarProgreso(t.validando);
                return Xrm.WebApi.online.execute(pedidoCatalogos(config, { archivo: base64 }));
            }).then(function (respuesta) {
                return respuesta.json();
            }).then(function (salida) {
                if (!salida || !salida.resultado) {
                    return { salida: salida || {}, falloDescarga: null };
                }
                mostrarProgreso(t.preparando);
                return abrirXlsx(salida.resultado, nombreDelResultado(archivo.name)).then(
                    function () { return { salida: salida, falloDescarga: null }; },
                    function (error) { return { salida: salida, falloDescarga: mensajeDeError(error) }; }
                );
            });
        }).then(function (resultado) {
            if (resultado.salida.grabado) {
                refrescar(control);
            }
            var texto = textoDelResumen(t, config, resultado.salida);
            if (resultado.falloDescarga) {
                texto = texto + "\n\n" + t.errorDescarga + " " + resultado.falloDescarga;
            }
            return Xrm.Navigation.openAlertDialog({ text: texto, confirmButtonLabel: t.cerrar });
        }).then(null, avisarError);
    }

    function cargarArchivoDeCatalogos(config, control) {
        elegirArchivo(function (archivo) {
            // Por la extension, sin distinguir mayusculas, ANTES de leerlo o llamar a la API.
            if (!/\.xlsx$/i.test(String(archivo.name))) {
                Xrm.Navigation.openAlertDialog({
                    text: idioma().extensionInvalida,
                    confirmButtonLabel: idioma().cerrar
                });
                return;
            }
            procesarArchivo(config, archivo, control);
        });
    }

    function descargarPlantillaDeCatalogos(config) {
        var t = idioma();
        return conProgreso(t.generandoPlantilla, function () {
            return Xrm.WebApi.online.execute(pedidoCatalogos(config, { plantilla: true })).then(function (respuesta) {
                return respuesta.json();
            }).then(function (salida) {
                if (!salida || !salida.resultado) {
                    throw new Error(t.sinArchivo);
                }
                return abrirXlsx(salida.resultado, config.plantilla);
            });
        }).then(null, avisarError);
    }

    /*
     * Los roles del usuario, sea cual sea la forma en que el cliente los entregue:
     * un array, o una Collection con `getAll()` o con `getLength()` + `get(i)`
     * (misma cautela que `filasDeLaGrilla`).
     */
    function rolesDelUsuario() {
        var roles = Xrm.Utility.getGlobalContext().userSettings.roles;
        if (!roles) { return []; }
        if (Array.isArray(roles)) { return roles; }
        if (typeof roles.getAll === "function") { return roles.getAll() || []; }
        var lista = [];
        var cuantos = typeof roles.getLength === "function" ? roles.getLength() : 0;
        for (var i = 0; i < cuantos; i++) {
            lista.push(roles.get(i));
        }
        return lista;
    }

    /*
     * Regla de visibilidad del ancla y de las opciones de los menus (DN-04).
     *
     * ANTE LA DUDA, `false` (al reves que `algunaAdmite`, que muestra). Alli un boton
     * oculto de mas deja a alguien sin poder trabajar; aca ocultar no le quita nada a
     * quien deba verlo, porque la puerta real es el privilegio de la Custom API
     * (`prvCreate`/`prvWrite` sobre Cliente, que solo tiene el Administrador de
     * planes): quien no lo tiene recibiria un rechazo del servidor igual.
     */
    function esAdministradorDePlanes(control) {
        try {
            var roles = rolesDelUsuario();
            for (var i = 0; i < roles.length; i++) {
                if (roles[i] && roles[i].name === ROL_ADMINISTRADOR_DE_PLANES) {
                    return true;
                }
            }
            return false;
        } catch (e) {
            return false;
        }
    }

    return {
        digitada: function (ids, control) {
            return ejecutar("Digitada", FILA.tabla, cambioDe(FILA, FILA.DIGITADA), ids, control);
        },
        aprobar: function (ids, control) {
            return ejecutar("Aprobar", FILA.tabla, cambioDe(FILA, FILA.APROBADA), ids, control);
        },
        atendido: function (ids, control) {
            return ejecutar("Atendido", SOLICITUD.tabla,
                cambioDe(SOLICITUD, SOLICITUD.CERRADA), ids, control);
        },
        descartar: function (ids, control) {
            return ejecutar("Descartar", SOLICITUD.tabla,
                cambioDe(SOLICITUD, SOLICITUD.DESCARTADA), ids, control);
        },

        // --- los cuatro que piden un texto ---
        rechazadaAs400: function (ids, control) {
            return ejecutarConMotivo("Rechazada en AS400", FILA.tabla, FILA.mensaje,
                cambioDe(FILA, FILA.RECHAZADA_AS400), ids, control);
        },
        anular: function (ids, control) {
            return ejecutarConMotivo("Anular", FILA.tabla, FILA.mensaje,
                cambioDe(FILA, FILA.ANULADA), ids, control);
        },
        devolver: function (ids, control) {
            // Devolver deja la fila otra vez en Validada, con el mensaje del
            // supervisor: es lo que la deja visible en "Devueltas".
            /*
             * Escribe en `sanic_notainterna`, NO en `sanic_mensaje` (2026-09-24).
             * La devolucion es un supervisor escribiendole al ejecutivo; el cliente
             * no tiene nada que ver. `sanic_mensaje` es la columna que el correo
             * muestra como "Motivos", y hasta ese dia la nota salia ahi: en correos
             * ya enviados, una fila APROBADA le llego al cliente con el motivo
             * "Devuelta".
             */
            return ejecutarConMotivo("Devolver", FILA.tabla, FILA.notaInterna,
                cambioDe(FILA, FILA.VALIDADA), ids, control);
        },
        revisado: function (ids, control) {
            /*
             * NO pide texto (corregido el 2026-09-23). Antes abria el dialogo de
             * motivo y escribia la nota en `sanic_motivorevision`. Dos problemas:
             *
             *  - la lista blanca no admite esa columna de una persona, asi que el
             *    boton fallaba SIEMPRE con "la(s) columna(s) sanic_motivorevision
             *    no esta(n) permitida(s)";
             *  - y esa columna no es del operador: la escriben los flujos para
             *    explicar POR QUE hace falta revisar ("Validacion fallida 3
             *    veces"). Es lo que el operador LEE. La nota lo habria pisado.
             *
             * Ahora solo apaga la marca. `RevisionAtendidaStep` toma el motivo que
             * ya estaba, lo deja en la Bitacora con quien y cuando, y limpia la
             * columna. Es lo que hace el mockup aprobado el 2026-09-20.
             */
            return ejecutar("Revisado", SOLICITUD.tabla,
                { sanic_requiererevision: false }, ids, control);
        },
        atendida: function (ids, control) {
            /*
             * Fase 2 (05, "Fase 2: dos vistas y un comando mas"). Apaga
             * sanic_requiereatencionmanual. NO pide texto, mismo motivo que
             * Revisado: es un boton de "ya lo resolvi", no una transicion de
             * FILA.columna (sanic_estado no se toca).
             *
             * La marca tambien se apaga sola con CUALQUIER transicion exitosa
             * de la fila (step de servidor, fuera de este archivo); este boton
             * es para el ejecutivo que resolvio el caso SIN cambiar el estado.
             *
             * No confundir con "atendido" (arriba): ese cierra la Solicitud.
             * Este apaga una marca de la Fila. Son dos botones, dos tablas.
             */
            return ejecutar("Atendida", FILA.tabla,
                { sanic_requiereatencionmanual: false }, ids, control);
        },
        descargarParaDigitar: descargarParaDigitar,

        // --- DN-04: menus de catalogos (el selector se abre SINCRONICAMENTE en el clic) ---
        esAdministradorDePlanes: esAdministradorDePlanes,
        cargarCatalogos: function (control) { cargarArchivoDeCatalogos(CATALOGOS.carga, control); },
        cambiarEstadoCatalogos: function (control) { cargarArchivoDeCatalogos(CATALOGOS.estado, control); },
        plantillaCarga: function (control) { return descargarPlantillaDeCatalogos(CATALOGOS.carga); },
        plantillaCambioEstado: function (control) { return descargarPlantillaDeCatalogos(CATALOGOS.estado); },

        /*
         * --- las reglas del ribbon (EnableRule / CustomRule) ---
         *
         * En Unified Interface un comando DESHABILITADO NO SE VE (Learn, "Define
         * ribbon enable rules": "commands that are disabled are hidden"), asi que
         * devolver `false` aca es lo que hace desaparecer el boton.
         *
         * Las cuatro acciones sobre la Solicitud (atendido, descartar, revisado) no
         * llevan regla: su estado no esta en las mismas grillas y hoy no hay un caso
         * reportado. Cuando lo haya, se agregan igual que estas.
         */
        puedeDigitada: function (control) { return algunaAdmite("digitada", control); },
        puedeAprobar: function (control) { return algunaAdmite("aprobar", control); },
        puedeDevolver: function (control) { return algunaAdmite("devolver", control); },
        puedeRechazadaAs400: function (control) { return algunaAdmite("rechazadaas400", control); },
        puedeAnular: function (control) { return algunaAdmite("anular", control); }
    };
})();
