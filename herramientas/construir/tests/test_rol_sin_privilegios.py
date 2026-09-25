"""La guarda de `rol.py` sobre un rol que no otorga NADA.

Nueve de cada diez veces, un rol sin privilegios propios es un playbook a medio
escribir: queda un clon del rol base con nombre nuevo que no habilita nada. La
décima es legítima y existe en este proyecto — `SR - MPPP - RPA` no da ACCESO,
da IDENTIDAD: la puerta de las dos Custom API de fase 2 (D-9) es TENER el rol.

Por eso la excepción NO es un flag de línea de comandos, que nadie vuelve a
leer: es un campo del playbook que exige la razón POR ESCRITO. Estas pruebas
fijan ese contrato (D-56, 2026-09-25)."""
import os
import sys
import unittest

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, _AQUI)

import rol as rl  # noqa: E402

IDENTIDAD = {"prefijo": "sanic", "abrev": "mppp"}
RAZON = ("Es un rol de IDENTIDAD, no de acceso: la puerta de las dos Custom API "
         "de fase 2 (D-9) es TENER este rol, no lo que el rol permite.")


def playbook(**extra):
    datos = {"tipo": "rol", "nombre": "SR - MPPP - RPA", "descripcion": "El bot.",
             "base": "App Opener", "tablas": {}, "otros_privilegios": {}}
    datos.update(extra)
    return datos


class RolSinPrivilegiosPropios(unittest.TestCase):
    def test_sin_el_campo_frena(self):
        """Sin justificación escrita, la guarda frena igual que siempre."""
        with self.assertRaises(rl.ErrorPlaybook) as e:
            rl.validar_playbook(playbook(), IDENTIDAD)
        self.assertIn("sin_privilegios_propios", str(e.exception))

    def test_con_una_razon_demasiado_corta_frena(self):
        """Un 'porque sí' no es una razón: el campo exige 40 caracteres o más."""
        with self.assertRaises(rl.ErrorPlaybook):
            rl.validar_playbook(playbook(sin_privilegios_propios="porque si"), IDENTIDAD)

    def test_en_blanco_frena(self):
        """Poner el campo vacío para saltear la guarda no cuenta como declararla."""
        with self.assertRaises(rl.ErrorPlaybook):
            rl.validar_playbook(playbook(sin_privilegios_propios="   " * 20), IDENTIDAD)

    def test_con_la_razon_escrita_pasa(self):
        """La excepción existe, y queda documentada DENTRO del artefacto."""
        rl.validar_playbook(playbook(sin_privilegios_propios=RAZON), IDENTIDAD)

    def test_la_guarda_no_afecta_a_un_rol_normal(self):
        """Un rol que sí otorga algo no necesita el campo, y tampoco le molesta."""
        rl.validar_playbook(playbook(tablas={"sanic_mppp_tbl_fila": {"leer": "organizacion"}}), IDENTIDAD)


if __name__ == "__main__":
    unittest.main()
