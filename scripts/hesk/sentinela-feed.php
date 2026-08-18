<?php
/**
 * Feed JSON de chamados para o Sentinela.
 * Copie este arquivo para a raiz do HESK (junto de hesk_settings.inc.php).
 */
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store');

const SENTINELA_FEED_TOKEN = 'sentinela-mobi-hesk';

$token = $_SERVER['HTTP_X_SENTINELA_TOKEN'] ?? ($_GET['token'] ?? '');
if (!hash_equals(SENTINELA_FEED_TOKEN, (string) $token)) {
    http_response_code(403);
    echo json_encode(['ok' => false, 'error' => 'token inválido'], JSON_UNESCAPED_UNICODE);
    exit;
}

define('IN_SCRIPT', 1);
define('HESK_PATH', __DIR__ . '/');

$settings = __DIR__ . '/hesk_settings.inc.php';
if (!is_file($settings)) {
    http_response_code(500);
    echo json_encode(['ok' => false, 'error' => 'hesk_settings.inc.php não encontrado'], JSON_UNESCAPED_UNICODE);
    exit;
}

require $settings;

$host = $hesk_settings['db_host'] ?? 'localhost';
$user = $hesk_settings['db_user'] ?? '';
$pass = $hesk_settings['db_pass'] ?? '';
$name = $hesk_settings['db_name'] ?? '';
$pfix = $hesk_settings['db_pfix'] ?? 'hesk_';
$heskUrl = rtrim($hesk_settings['hesk_url'] ?? 'http://menu/chamados', '/');
$adminBase = $heskUrl . '/admin';

mysqli_report(MYSQLI_REPORT_OFF);
$db = @new mysqli($host, $user, $pass, $name);
if ($db->connect_errno) {
    http_response_code(502);
    echo json_encode(['ok' => false, 'error' => 'falha ao conectar no banco do HESK'], JSON_UNESCAPED_UNICODE);
    exit;
}
$db->set_charset('utf8mb4');

$ticketsTable = $pfix . 'tickets';
$catTable = $pfix . 'categories';
$select = "SELECT t.id, t.trackid, t.u_name, t.u_email, t.category, t.priority, t.subject, t.status, t.dt, t.lastchange, c.name AS category_name
        FROM `{$ticketsTable}` t
        LEFT JOIN `{$catTable}` c ON c.id = t.category";

$openSql = $select . " WHERE t.status <> 3 ORDER BY t.dt ASC LIMIT 100";
$closedSql = $select . " WHERE t.status = 3 ORDER BY t.lastchange DESC LIMIT 12";

$openResult = $db->query($openSql);
$closedResult = $db->query($closedSql);
if ($openResult === false || $closedResult === false) {
    http_response_code(500);
    echo json_encode(['ok' => false, 'error' => 'falha ao ler tickets'], JSON_UNESCAPED_UNICODE);
    $db->close();
    exit;
}

$countRes = $db->query("SELECT COUNT(*) AS n FROM `{$ticketsTable}` WHERE status <> 3");
$openCount = 0;
if ($countRes) {
    $openCount = (int) ($countRes->fetch_assoc()['n'] ?? 0);
    $countRes->free();
}

$statusLabels = [
    0 => 'Novo',
    1 => 'Aguardando',
    2 => 'Respondido',
    3 => 'Resolvido',
    4 => 'Em andamento',
    5 => 'Em espera',
];
$priorityLabels = [
    0 => 'Crítica',
    1 => 'Alta',
    2 => 'Média',
    3 => 'Baixa',
];

$tz = new DateTimeZone('America/Sao_Paulo');

$mapRow = static function (array $row) use ($tz, $statusLabels, $priorityLabels, $adminBase) {
    $status = (int) $row['status'];
    $priority = (int) $row['priority'];
    $event = 'updated';
    if ($status === 0) $event = 'new';
    elseif ($status === 1) $event = 'waiting';
    elseif ($status === 2) $event = 'reply';
    elseif ($status === 3) $event = 'resolved';
    elseif ($status === 4) $event = 'progress';
    elseif ($status === 5) $event = 'hold';
    $created = new DateTime($row['dt'], $tz);
    $updated = new DateTime($row['lastchange'], $tz);
    $track = (string) $row['trackid'];
    return [
        'id' => (int) $row['id'],
        'trackId' => $track,
        'subject' => (string) $row['subject'],
        'name' => (string) $row['u_name'],
        'email' => (string) $row['u_email'],
        'status' => $status,
        'statusLabel' => $statusLabels[$status] ?? ('Status ' . $status),
        'priority' => $priority,
        'priorityLabel' => $priorityLabels[$priority] ?? ('P' . $priority),
        'category' => (string) ($row['category_name'] ?: $row['category']),
        'createdAt' => $created->format('c'),
        'updatedAt' => $updated->format('c'),
        'event' => $event,
        'url' => $adminBase . '/admin_ticket.php?track=' . rawurlencode($track),
    ];
};

$tickets = [];
while ($row = $openResult->fetch_assoc()) {
    $tickets[] = $mapRow($row);
}
$openResult->free();
while ($row = $closedResult->fetch_assoc()) {
    $tickets[] = $mapRow($row);
}
$closedResult->free();
$db->close();

echo json_encode([
    'ok' => true,
    'openCount' => $openCount,
    'tickets' => $tickets,
], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
