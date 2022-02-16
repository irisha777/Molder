#@ignore
@SqlServer
Feature: DatabaseAsyncExamples
	Background: 
		Given я асинхронно подключаюсь к БД MS SQL Server с названием "QA":
		| DataSource | InitialCatalog | UserID    | Password     |
		| {{SOURCE}} | {{DATABASE}}   | {{LOGIN}} | {{PASSWORD}} |

Scenario: Async SELECT
	Given я выполняю асинхронный "SELECT" запрос в БД "QA" и сохраняю результат в переменную "result":
"""
SELECT * FROM Products
"""
Scenario: Async SELECT ONE
	Given я асинхронно выбираю единственную запись из БД "QA" и сохраняю её в переменную "result":
"""
SELECT TOP 1 * FROM Products
"""
	Then write variable "result[1]"
	Then write variable "result[name]"

Scenario: Async INSERT
	Given я сохраняю случайный набор цифр длиной 5 знаков в переменную "id"
	Given я сохраняю случайный набор букв длиной 5 знаков в переменную "name"
	Given я выполняю асинхронный "INSERT" запрос в БД "QA" и сохраняю результат в переменную "result":
"""
INSERT Products (id, ProductName) VALUES (344, '234');
"""