# Operation Dream — Multiplayer 3D FPS Prototype (Unity, Mirror)

## Overview
Operation Dream is a Unity-based 3D Multiplayer First-Person Shooter (FPS) prototype designed around LAN (Host/Client) networking. 
The project demonstrates player movement, health and regeneration systems, gunplay mechanics with weapon recoil/ADS, network state synchronization via Mirror Networking, dynamic HUD elements, and procedural terrain environments.

This prototype was created as a technical portfolio project to showcase multiplayer systems architecture, networking logic, and game testing (QA) practices.

---

## Features
- **LAN Multiplayer Networking:** Host / Join system built with Mirror Networking.
- **Player Controller:** `CharacterController`-based movement with local & network authority separation.
- **Health & Regeneration System:** Dynamic vertical HP bar on HUD, dynamic color shifting based on current HP, and auto-regeneration.
- **Weapon System (`WeaponController`):** Shooting, reloading, weapon switching, and HUD ammo counter.
- **Weapon Mechanics:** Recoil management (`RecoilHandler`), Aim Down Sights (`WeaponADS`), and scriptable settings (`WeaponData`).
- **Network Synchronization:**
  - Player transform and state synchronization (`NetworkTransform`, `SyncVar`).
  - Networked ADS and aiming state sync.
  - Networked sound synchronization (footsteps, gunfire, reloading).
  - Authority fixes for player movement and weapon switching across clients.
- **Environment & Map:** Terrain-based map with trees, grass, optimized colliders, and terrain performance tweaks.
- **Death & Respawn Logic:** Player death state processing and post-death menu interface.
- **Player Customization:** Support for multiple player skins and camouflage variants.

---

## Systems Architecture
Core gameplay and networking systems are separated into modular C# scripts:

- **`NetworkHealth.cs`** — Handles synced health state via `SyncVar` and damage execution across network clients.
- **`WeaponController.cs`** — Manages shooting, reloading, and weapon state machines.
- **`RecoilHandler.cs`** — Calculates and applies procedural weapon recoil.
- **`WeaponADS.cs`** — Handles Aim Down Sights transitions and FOV shifting.
- **`WeaponData.cs`** — `ScriptableObject` containing weapon statistics, ammo capacity, and fire rates.
- **`PlayerHUD.cs`** — Updates ammo count, reload text, and dynamic HP bar visualization.
- **`DeathMenuUI.cs`** — Post-death UI flow and respawn trigger logic.

---

## My Responsibilities & QA Testing
Designed, implemented, and tested:
- Client-server networking setup using Mirror Networking framework.
- Network state synchronization (`SyncVar`, `NetworkTransform`) for player movement, audio, and weapon states.
- Local multiplayer build testing with external testers to find and fix desynchronization bugs (desync), input authority leaking, and audio desync.
- FPS weapon mechanics, recoil handling, ADS, and health regeneration logic.
- Terrain collider optimization and environment setup.
- UI/HUD integration for ammunition, reload states, and dynamic health display.

---

## Technologies Used
- **Engine:** Unity (C#)
- **Networking:** Mirror Networking (Host / Join LAN model)
- **Architecture:** Component-based modular scripting, `ScriptableObject` data structures
- **Version Control:** Git & GitHub

---

## Project Purpose
This project was built as a hands-on Unity gameplay and networking prototype. 
It demonstrates overall game systems architecture, C# scripting, and the practical debugging/testing process required to build and polish multiplayer gameplay mechanics.

---

YouTube (First version) - https://youtube.com/playlist?list=PLdo3vw5eR14U&si=UrCT2F5pG_C3InYc

---
---

# Operation Dream — Процедурный 3D Мультиплеерный FPS Прототип

## О проекте
Operation Dream — это прототип 3D-шутера от первого лица (FPS) на Unity с поддержкой локального мультиплеера (LAN / Host-Client). 
Проект демонстрирует физику персонажа, систему здоровья и регенерации, механику стрельбы с отдачей и прицеливанием (ADS), сетевую синхронизацию через Mirror Networking, динамический HUD и оптимизированное окружение на базе Terrain.

Этот проект создан как портфолио-прототип для демонстрации навыков разработки сетевых игровых систем на Unity и практики QA-тестирования сетевых билдов.

---

## Возможности проекта
- **Сетевой мультиплеер:** Локальная система подключения (Host / Join) на базе Mirror Networking.
- **Управление игроком:** Движение на базе `CharacterController` с разделением локального и сетевого авторитета.
- **Система здоровья:** Вертикальный HP-бар на HUD, динамическое изменение цвета в зависимости от HP, автоматическая регенерация.
- **Система оружия (`WeaponController`):** Стрельба, перезарядка, переключение оружия, отображение боезапаса на HUD.
- **Механики оружия:** Обработка отдачи (`RecoilHandler`), прицеливание (`WeaponADS`), конфигурирование через `WeaponData`.
- **Сетевая синхронизация:**
  - Синхронизация позиции и состояний игроков (`NetworkTransform`, `SyncVar`).
  - Сетевая синхронизация прицеливания (ADS).
  - Сетевая синхронизация звуков (шаги, выстрелы, перезарядка).
  - Исправление багов управления и переключения оружия по сети.
- **Окружение и карта:** Карта на Terrain (трава, деревья, настроенные коллизии, оптимизация).
- **Логика смерти:** Система обработки смерти игрока и меню после смерти.
- **Кастомизация:** Поддержка нескольких скинов/камуфляжей для персонажей.

---

## Архитектура систем
Основные системы разделены на отдельные модульные C#-скрипты:

- **`NetworkHealth.cs`** — синхронизация здоровья через `SyncVar` и получение урона по сети.
- **`WeaponController.cs`** — логика стрельбы, перезарядки и смены оружия.
- **`RecoilHandler.cs`** — расчёт и примянение отдачи при стрельбе.
- **`WeaponADS.cs`** — прицеливание и изменение угла обзора (FOV).
- **`WeaponData.cs`** — `ScriptableObject` с настройками характеристик оружия.
- **`PlayerHUD.cs`** — обновление интерфейса (патроны, здоровье, текст перезарядки).
- **`DeathMenuUI.cs`** — логика экрана смерти и возрождения.

---

## Что реализовано лично & QA Тестирование
В рамках проекта реализованы и протестированы:
- Настройка и интеграция клиент-серверной сетевой логики на базе Mirror.
- Сетевая синхронизация состояний (`SyncVar`, `NetworkTransform`) для перемещения, звуков и оружия.
- Проведение сетевых тестов локальных билдов с внешними тестерами, поиск и исправление сетевых багов (рассинхрон состояний, ошибки передачи управления, звуковые десинхроны).
- Механики FPS-оружия, отдача, прицеливание и регенерация здоровья.
- Оптимизация Terrain-карты и коллизий.
- UI/HUD система для отображения патронов и динамического HP.

---

## Технологии
- **Движок:** Unity (C#)
- **Сеть:** Mirror Networking (Host / Join LAN)
- **Архитектура:** Модульная компонентная архитектура, `ScriptableObjects`
- **Контроль версий:** Git / GitHub

---

## Назначение проекта
Проект создан как практический прототип на Unity для демонстрации архитектуры игровых систем, C#-скриптинга и полного цикла разработки, отладки и тестирования сетевых игровых механик.

---

YouTube (First version) - https://youtube.com/playlist?list=PLdo3vw5eR14U&si=UrCT2F5pG_C3InYc
